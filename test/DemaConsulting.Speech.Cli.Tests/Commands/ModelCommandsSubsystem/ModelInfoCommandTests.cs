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
///     Unit tests for <see cref="ModelInfoCommand"/>, using <see cref="FakeCliModelCatalog"/> so
///     every scenario runs deterministically with no real catalog or network access.
/// </summary>
[Collection("Sequential")]
public sealed class ModelInfoCommandTests
{
    /// <summary>
    ///     Test that a known model's identity, state, license, and audio-tag support are printed.
    /// </summary>
    [Fact]
    public void ModelInfoCommand_Run_KnownModel_PrintsIdentityAndState()
    {
        // Arrange
        var model = new FakeSpeechModel(
            "model-1",
            "Model One",
            SpeechModelRole.Synthesis,
            audioTagSupport: SpeechModelAudioTagSupport.Native,
            licenseName: "Apache-2.0",
            licenseUrl: new Uri("https://example.test/license"));
        var catalog = new FakeCliModelCatalog().WithModel(model, SpeechModelState.Downloaded);
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["model-info", "model-1"]);

            // Act
            ModelInfoCommand.Run(context, catalog);

            // Assert
            var output = writer.ToString();
            Assert.Contains("Id: model-1", output);
            Assert.Contains("Display Name: Model One", output);
            Assert.Contains("Role: Synthesis", output);
            Assert.Contains("State: Downloaded", output);
            Assert.Contains("License: Apache-2.0 (https://example.test/license)", output);
            Assert.Contains("Audio Tag Support: Native", output);
            Assert.Contains("Parameters: (none)", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that a numeric parameter's min/max/step/default/unit are printed.
    /// </summary>
    [Fact]
    public void ModelInfoCommand_Run_NumericParameter_PrintsBounds()
    {
        // Arrange
        var parameter = new NumericParameter(
            "rate",
            "Speaking rate",
            "Controls how fast speech is spoken.",
            new NumericParameterBounds(0.5, 2.0, 0.1, 1.0),
            unit: "x");
        var model = new FakeSpeechModel("model-numeric", parameters: [parameter]);
        var catalog = new FakeCliModelCatalog().WithModel(model, SpeechModelState.NotDownloaded);
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["model-info", "model-numeric"]);

            // Act
            ModelInfoCommand.Run(context, catalog);

            // Assert
            var output = writer.ToString();
            Assert.Contains("rate (Speaking rate)", output);
            Assert.Contains("Controls how fast speech is spoken.", output);
            Assert.Contains("min=0.5, max=2, step=0.1, default=1", output);
            Assert.Contains("x", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that a choice parameter's options and default are printed.
    /// </summary>
    [Fact]
    public void ModelInfoCommand_Run_ChoiceParameter_PrintsOptions()
    {
        // Arrange
        var parameter = new ChoiceParameter(
            "voice",
            "Voice",
            "Selects the voice.",
            [new ChoiceParameterOption("a", "Voice A"), new ChoiceParameterOption("b", "Voice B")],
            "a");
        var model = new FakeSpeechModel("model-choice", parameters: [parameter]);
        var catalog = new FakeCliModelCatalog().WithModel(model, SpeechModelState.NotDownloaded);
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["model-info", "model-choice"]);

            // Act
            ModelInfoCommand.Run(context, catalog);

            // Assert
            var output = writer.ToString();
            Assert.Contains("voice (Voice)", output);
            Assert.Contains("a (Voice A)", output);
            Assert.Contains("b (Voice B)", output);
            Assert.Contains("default=a", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that a boolean parameter's default is printed.
    /// </summary>
    [Fact]
    public void ModelInfoCommand_Run_BooleanParameter_PrintsDefault()
    {
        // Arrange
        var parameter = new BooleanParameter("denoise", "Denoise", "Removes background noise.", true);
        var model = new FakeSpeechModel("model-bool", parameters: [parameter]);
        var catalog = new FakeCliModelCatalog().WithModel(model, SpeechModelState.NotDownloaded);
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["model-info", "model-bool"]);

            // Act
            ModelInfoCommand.Run(context, catalog);

            // Assert
            var output = writer.ToString();
            Assert.Contains("denoise (Denoise)", output);
            Assert.Contains("Type: boolean, default=True", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that an unknown model id throws a clean <see cref="ArgumentException"/> naming
    ///     the id and pointing at <c>list-models</c>.
    /// </summary>
    [Fact]
    public void ModelInfoCommand_Run_UnknownModelId_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["model-info", "does-not-exist"]);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => ModelInfoCommand.Run(context, catalog));
        Assert.Contains("Unknown model id 'does-not-exist'", ex.Message);
        Assert.Contains("list-models", ex.Message);
    }

    /// <summary>
    ///     Test that omitting the required <c>&lt;modelId&gt;</c> argument throws.
    /// </summary>
    [Fact]
    public void ModelInfoCommand_Run_MissingModelIdArgument_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["model-info"]);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ModelInfoCommand.Run(context, catalog));
    }

    /// <summary>
    ///     Test that a second, unsupported positional argument throws.
    /// </summary>
    [Fact]
    public void ModelInfoCommand_Run_ExtraArgument_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["model-info", "model-1", "extra"]);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ModelInfoCommand.Run(context, catalog));
    }

    /// <summary>
    ///     Test that a null context is rejected.
    /// </summary>
    [Fact]
    public void ModelInfoCommand_Run_NullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ModelInfoCommand.Run(null!, new FakeCliModelCatalog()));
    }

    /// <summary>
    ///     Test that a null catalog is rejected.
    /// </summary>
    [Fact]
    public void ModelInfoCommand_Run_NullCatalog_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["model-info", "model-1"]);
        Assert.Throws<ArgumentNullException>(() => ModelInfoCommand.Run(context, null!));
    }
}
