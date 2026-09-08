// Copyright (c) DEMA Consulting
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using DemaConsulting.Speech.Cli.Cli;
using DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.ModelCommandsSubsystem;

/// <summary>
///     Unit tests for <see cref="ListModelsCommand"/>, using <see cref="FakeCliModelCatalog"/> so
///     every scenario runs deterministically with no real catalog or network access.
/// </summary>
[Collection("Sequential")]
public sealed class ListModelsCommandTests
{
    /// <summary>
    ///     Test that the default table format lists every known model with its role and state.
    /// </summary>
    [Fact]
    public void ListModelsCommand_Run_NoFilters_ListsEveryModelAsTable()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog()
            .WithModel(new FakeSpeechModel("stt-1", "STT One", SpeechModelRole.Recognition), SpeechModelState.NotDownloaded)
            .WithModel(new FakeSpeechModel("tts-1", "TTS One", SpeechModelRole.Synthesis, licenseName: "MIT"), SpeechModelState.Downloaded);
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["list-models"]);

            // Act
            ListModelsCommand.Run(context, catalog);

            // Assert
            var output = writer.ToString();
            Assert.Contains("stt-1", output);
            Assert.Contains("STT One", output);
            Assert.Contains("Recognition", output);
            Assert.Contains("NotDownloaded", output);
            Assert.Contains("tts-1", output);
            Assert.Contains("MIT", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that <c>--role tts</c> filters down to synthesis models only.
    /// </summary>
    [Fact]
    public void ListModelsCommand_Run_RoleFilterTts_ListsOnlySynthesisModels()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog()
            .WithModel(new FakeSpeechModel("stt-1", role: SpeechModelRole.Recognition), SpeechModelState.NotDownloaded)
            .WithModel(new FakeSpeechModel("tts-1", role: SpeechModelRole.Synthesis), SpeechModelState.NotDownloaded);
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["list-models", "--role", "tts"]);

            // Act
            ListModelsCommand.Run(context, catalog);

            // Assert
            var output = writer.ToString();
            Assert.Contains("tts-1", output);
            Assert.DoesNotContain("stt-1", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that <c>--state downloaded</c> filters down to installed models only.
    /// </summary>
    [Fact]
    public void ListModelsCommand_Run_StateFilterDownloaded_ListsOnlyDownloadedModels()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog()
            .WithModel(new FakeSpeechModel("model-missing"), SpeechModelState.NotDownloaded)
            .WithModel(new FakeSpeechModel("model-installed"), SpeechModelState.Downloaded);
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["list-models", "--state", "downloaded"]);

            // Act
            ListModelsCommand.Run(context, catalog);

            // Assert
            var output = writer.ToString();
            Assert.Contains("model-installed", output);
            Assert.DoesNotContain("model-missing", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that <c>--state missing</c> filters down to not-yet-downloaded models only.
    /// </summary>
    [Fact]
    public void ListModelsCommand_Run_StateFilterMissing_ListsOnlyMissingModels()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog()
            .WithModel(new FakeSpeechModel("model-missing"), SpeechModelState.NotDownloaded)
            .WithModel(new FakeSpeechModel("model-installed"), SpeechModelState.Downloaded);
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["list-models", "--state", "missing"]);

            // Act
            ListModelsCommand.Run(context, catalog);

            // Assert
            var output = writer.ToString();
            Assert.Contains("model-missing", output);
            Assert.DoesNotContain("model-installed", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that <c>--format json</c> emits a JSON array with the expected fields.
    /// </summary>
    [Fact]
    public void ListModelsCommand_Run_FormatJson_EmitsJsonArray()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog()
            .WithModel(new FakeSpeechModel("model-1", "Model One", licenseName: "Apache-2.0"), SpeechModelState.NotDownloaded);
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["list-models", "--format", "json"]);

            // Act
            ListModelsCommand.Run(context, catalog);

            // Assert
            var output = writer.ToString();
            Assert.Contains("\"Id\": \"model-1\"", output);
            Assert.Contains("\"DisplayName\": \"Model One\"", output);
            Assert.Contains("\"LicenseName\": \"Apache-2.0\"", output);
            Assert.Contains("[", output);
            Assert.Contains("]", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that an empty catalog with the default format prints an honest "no models" message
    ///     rather than an empty table or a crash.
    /// </summary>
    [Fact]
    public void ListModelsCommand_Run_EmptyCatalog_PrintsNoModelsMessage()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["list-models"]);

            // Act
            ListModelsCommand.Run(context, catalog);

            // Assert
            Assert.Contains("No models match", writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that an invalid <c>--role</c> value is rejected with a clean <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void ListModelsCommand_Run_InvalidRoleValue_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["list-models", "--role", "bogus"]);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ListModelsCommand.Run(context, catalog));
    }

    /// <summary>
    ///     Test that an invalid <c>--format</c> value is rejected with a clean <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void ListModelsCommand_Run_InvalidFormatValue_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["list-models", "--format", "bogus"]);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ListModelsCommand.Run(context, catalog));
    }

    /// <summary>
    ///     Test that an unsupported flag is rejected with a clean <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void ListModelsCommand_Run_UnsupportedFlag_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["list-models", "--bogus"]);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ListModelsCommand.Run(context, catalog));
    }

    /// <summary>
    ///     Test that a flag missing its required value is rejected with a clean <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void ListModelsCommand_Run_RoleFlagMissingValue_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["list-models", "--role"]);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ListModelsCommand.Run(context, catalog));
    }

    /// <summary>
    ///     Test that a null context is rejected.
    /// </summary>
    [Fact]
    public void ListModelsCommand_Run_NullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ListModelsCommand.Run(null!, new FakeCliModelCatalog()));
    }

    /// <summary>
    ///     Test that a null catalog is rejected.
    /// </summary>
    [Fact]
    public void ListModelsCommand_Run_NullCatalog_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["list-models"]);
        Assert.Throws<ArgumentNullException>(() => ListModelsCommand.Run(context, null!));
    }
}
