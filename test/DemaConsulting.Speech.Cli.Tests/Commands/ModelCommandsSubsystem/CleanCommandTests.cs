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
///     Unit tests for <see cref="CleanCommand"/>, using <see cref="FakeCliModelCatalog"/> so
///     every scenario runs deterministically with no real store access.
/// </summary>
[Collection("Sequential")]
public sealed class CleanCommandTests
{
    /// <summary>
    ///     Test that <c>clean</c> forwards to the catalog seam's cleanup and reports success.
    /// </summary>
    [Fact]
    public async Task CleanCommand_Run_KnownModel_CleansUpAndReportsSuccess()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog().WithModel(new FakeSpeechModel("model-1"), SpeechModelState.Downloaded);
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["clean", "model-1"]);

            // Act
            await CleanCommand.RunAsync(context, catalog, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["model-1"], catalog.CleanUpLeftoversCalls);
            Assert.Contains("model-1", writer.ToString());
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that a canceled cleanup is reported as an actionable error naming the model,
    ///     rather than propagating the cancellation.
    /// </summary>
    [Fact]
    public async Task CleanCommand_Run_Canceled_ReportsErrorExitCode()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["clean", "model-1"]);
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);

        // Act
        try
        {
            await CleanCommand.RunAsync(context, catalog, cancellation.Token);
        }
        finally
        {
            Console.SetError(originalError);
        }

        // Assert
        Assert.Equal(1, context.ExitCode);
        Assert.Contains("Cleaning up model 'model-1' was canceled.", errorWriter.ToString());
    }

    /// <summary>
    ///     Test that a cancellation observed while the cleanup is in flight (not just before it
    ///     starts) is reported as an actionable error with a non-zero exit code.
    /// </summary>
    [Fact]
    public async Task CleanCommand_Run_CanceledDuringCleanup_ReportsErrorExitCode()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var catalog = new FakeCliModelCatalog { CleanUpBlocksUntilCanceled = true }
            .WithModel(new FakeSpeechModel("model-1"), SpeechModelState.Downloaded);
        using var context = Context.Create(["clean", "model-1"]);
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);

        // Act
        try
        {
            var run = CleanCommand.RunAsync(context, catalog, cancellation.Token);
            await catalog.CleanUpStarted.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await cancellation.CancelAsync();
            await run;
        }
        finally
        {
            Console.SetError(originalError);
        }

        // Assert
        Assert.Equal(["model-1"], catalog.CleanUpLeftoversCalls);
        Assert.Equal(1, context.ExitCode);
        Assert.Contains("Cleaning up model 'model-1' was canceled.", errorWriter.ToString());
    }

    /// <summary>
    ///     Test that, through the real store, <c>clean &lt;modelId&gt;</c> removes a partial
    ///     (interrupted-download) staging artifact and a superseded install directory, and succeeds.
    /// </summary>
    [Fact]
    public async Task CleanCommand_Run_RealStoreWithPartialArtifact_RemovesArtifactAndSucceeds()
    {
        // Arrange
        var root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Cli.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var options = new SpeechModelStoreOptions { RootPathOverride = root };
            using var adapter = new SpeechModelCatalogAdapter(options);
            var modelId = adapter.Enumerate()[0].Id;
            var modelDirectory = new SpeechModelStore(options).GetModelDirectory(modelId);
            var stagingDirectory = Path.Join(modelDirectory, ".tmp", "interrupted-op");
            var replacedDirectory = Path.Join(modelDirectory, "current.replaced-stale");
            Directory.CreateDirectory(stagingDirectory);
            Directory.CreateDirectory(replacedDirectory);
            await File.WriteAllBytesAsync(Path.Join(stagingDirectory, "model.bin.partial"), [1, 2, 3], TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Join(replacedDirectory, "old.bin"), [4, 5, 6], TestContext.Current.CancellationToken);
            using var context = Context.Create(["clean", modelId]);

            // Act
            await CleanCommand.RunAsync(context, adapter, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, context.ExitCode);
            Assert.False(Directory.Exists(stagingDirectory));
            Assert.False(Directory.Exists(replacedDirectory));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Test that omitting the required <c>&lt;modelId&gt;</c> argument throws.
    /// </summary>
    [Fact]
    public async Task CleanCommand_Run_MissingModelIdArgument_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["clean"]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => CleanCommand.RunAsync(context, catalog, TestContext.Current.CancellationToken));
        Assert.Null(ex.ParamName);
    }

    /// <summary>
    ///     Test that a second, unsupported positional argument throws.
    /// </summary>
    [Fact]
    public async Task CleanCommand_Run_ExtraArgument_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["clean", "model-1", "extra"]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => CleanCommand.RunAsync(context, catalog, TestContext.Current.CancellationToken));
        Assert.Null(ex.ParamName);
    }

    /// <summary>
    ///     Test that a null context is rejected.
    /// </summary>
    [Fact]
    public async Task CleanCommand_Run_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => CleanCommand.RunAsync(null!, catalog, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that a null catalog is rejected.
    /// </summary>
    [Fact]
    public async Task CleanCommand_Run_NullCatalog_ThrowsArgumentNullException()
    {
        // Arrange
        using var context = Context.Create(["clean", "model-1"]);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => CleanCommand.RunAsync(context, null!, TestContext.Current.CancellationToken));
    }
}
