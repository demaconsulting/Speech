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
///     Unit tests for <see cref="DownloadCommand"/>, using <see cref="FakeCliModelCatalog"/> so
///     every scenario runs deterministically with no real catalog, network access, or download.
/// </summary>
[Collection("Sequential")]
public sealed class DownloadCommandTests
{
    /// <summary>
    ///     Test that a successful download of a single model reports installation and leaves the
    ///     exit code clean.
    /// </summary>
    [Fact]
    public async Task DownloadCommand_RunAsync_SingleModel_Succeeds_ReportsInstalled()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog().WithModel(new FakeSpeechModel("model-1"), SpeechModelState.NotDownloaded);
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["download", "model-1"]);

            // Act
            await DownloadCommand.RunAsync(context, catalog, CancellationToken.None);

            // Assert
            var output = writer.ToString();
            Assert.Contains("Downloading model 'model-1'", output);
            Assert.Contains("Model 'model-1' installed successfully.", output);
            Assert.Equal(0, context.ExitCode);
            Assert.Equal(["model-1"], catalog.DownloadCalls);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that multiple requested model ids are downloaded sequentially, in order.
    /// </summary>
    [Fact]
    public async Task DownloadCommand_RunAsync_MultipleModels_DownloadsEachSequentially()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog()
            .WithModel(new FakeSpeechModel("model-1"), SpeechModelState.NotDownloaded)
            .WithModel(new FakeSpeechModel("model-2"), SpeechModelState.NotDownloaded);
        using var context = Context.Create(["download", "model-1", "model-2"]);

        // Act
        await DownloadCommand.RunAsync(context, catalog, CancellationToken.None);

        // Assert
        Assert.Equal(["model-1", "model-2"], catalog.DownloadCalls);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test that a download reported as a non-installed outcome sets a non-zero exit code
    ///     without throwing, and still lets remaining requested models proceed.
    /// </summary>
    [Fact]
    public async Task DownloadCommand_RunAsync_ChecksumMismatch_ReportsErrorAndContinues()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog
        {
            DownloadResultFactory = _ => new SpeechModelDownloadResult(SpeechModelDownloadOutcome.ChecksumMismatch),
        };
        catalog.WithModel(new FakeSpeechModel("model-1"), SpeechModelState.NotDownloaded);
        catalog.WithModel(new FakeSpeechModel("model-2"), SpeechModelState.NotDownloaded);
        using var context = Context.Create(["download", "model-1", "model-2"]);

        // Act
        await DownloadCommand.RunAsync(context, catalog, CancellationToken.None);

        // Assert: both models were still attempted, and the exit code reflects the failure
        Assert.Equal(["model-1", "model-2"], catalog.DownloadCalls);
        Assert.Equal(1, context.ExitCode);
    }

    /// <summary>
    ///     Test that an unknown model id is reported as a failure but does not abort remaining
    ///     requested models.
    /// </summary>
    [Fact]
    public async Task DownloadCommand_RunAsync_UnknownModelId_ReportsErrorAndContinues()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog().WithModel(new FakeSpeechModel("model-2"), SpeechModelState.NotDownloaded);
        using var context = Context.Create(["download", "does-not-exist", "model-2"]);

        // Act
        await DownloadCommand.RunAsync(context, catalog, CancellationToken.None);

        // Assert
        Assert.Equal(["does-not-exist", "model-2"], catalog.DownloadCalls);
        Assert.Equal(1, context.ExitCode);
    }

    /// <summary>
    ///     Test that <c>--force</c> uninstalls an already-downloaded model before downloading it
    ///     again, since <c>DownloadAsync</c> is otherwise a no-op for an already-installed model.
    /// </summary>
    [Fact]
    public async Task DownloadCommand_RunAsync_ForceOnDownloadedModel_UninstallsFirst()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog().WithModel(new FakeSpeechModel("model-1"), SpeechModelState.Downloaded);
        using var context = Context.Create(["download", "model-1", "--force"]);

        // Act
        await DownloadCommand.RunAsync(context, catalog, CancellationToken.None);

        // Assert
        Assert.Equal(["model-1"], catalog.UninstallCalls);
        Assert.Equal(["model-1"], catalog.DownloadCalls);
    }

    /// <summary>
    ///     Test that <c>--force</c> does not attempt an uninstall for a model that is not
    ///     currently downloaded.
    /// </summary>
    [Fact]
    public async Task DownloadCommand_RunAsync_ForceOnMissingModel_DoesNotUninstall()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog().WithModel(new FakeSpeechModel("model-1"), SpeechModelState.NotDownloaded);
        using var context = Context.Create(["download", "model-1", "--force"]);

        // Act
        await DownloadCommand.RunAsync(context, catalog, CancellationToken.None);

        // Assert
        Assert.Empty(catalog.UninstallCalls);
        Assert.Equal(["model-1"], catalog.DownloadCalls);
    }

    /// <summary>
    ///     Test that progress reports are forwarded through to the caller-supplied progress sink.
    /// </summary>
    /// <remarks>
    ///     <see cref="Progress{T}"/> marshals its callback through the
    ///     <see cref="SynchronizationContext"/> captured at construction (falling back to the
    ///     thread pool when none is current), so this test installs a synchronous context for
    ///     the duration of the call to make the callback's completion deterministic instead of
    ///     racing a background thread-pool callback against the assertion below.
    /// </remarks>
    [Fact]
    public async Task DownloadCommand_RunAsync_ReportsProgress_WritesPercentLine()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog
        {
            OnDownload = (_, progress) => progress?.Report(new SpeechModelDownloadProgress(0, 1, 50, 100)),
        };
        catalog.WithModel(new FakeSpeechModel("model-1"), SpeechModelState.NotDownloaded);
        var originalOut = Console.Out;
        var originalSyncContext = SynchronizationContext.Current;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            SynchronizationContext.SetSynchronizationContext(new ImmediateSynchronizationContext());
            using var context = Context.Create(["download", "model-1"]);

            // Act
            await DownloadCommand.RunAsync(context, catalog, CancellationToken.None);

            // Assert
            Assert.Contains("50%", writer.ToString());
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalSyncContext);
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     A <see cref="SynchronizationContext"/> that runs every posted or sent callback
    ///     synchronously and inline, used only to make <see cref="Progress{T}"/> callback
    ///     delivery deterministic in tests.
    /// </summary>
    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        /// <inheritdoc/>
        public override void Post(SendOrPostCallback d, object? state) => d(state);

        /// <inheritdoc/>
        public override void Send(SendOrPostCallback d, object? state) => d(state);
    }

    /// <summary>
    ///     Test that a canceled download is reported cleanly and stops the remaining batch.
    /// </summary>
    [Fact]
    public async Task DownloadCommand_RunAsync_Canceled_ReportsErrorAndStopsBatch()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var catalog = new FakeCliModelCatalog()
            .WithModel(new FakeSpeechModel("model-1"), SpeechModelState.NotDownloaded)
            .WithModel(new FakeSpeechModel("model-2"), SpeechModelState.NotDownloaded);
        using var context = Context.Create(["download", "model-1", "model-2"]);

        // Act
        await DownloadCommand.RunAsync(context, catalog, cts.Token);

        // Assert: canceled before it started - nothing was attempted, and the exit code reflects it
        Assert.Empty(catalog.DownloadCalls);
        Assert.Equal(1, context.ExitCode);
    }

    /// <summary>
    ///     Test that no requested model id throws a clean <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public async Task DownloadCommand_RunAsync_NoModelIds_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["download"]);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => DownloadCommand.RunAsync(context, catalog, CancellationToken.None));
    }

    /// <summary>
    ///     Test that an unsupported flag throws a clean <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public async Task DownloadCommand_RunAsync_UnsupportedFlag_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["download", "model-1", "--bogus"]);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => DownloadCommand.RunAsync(context, catalog, CancellationToken.None));
    }

    /// <summary>
    ///     Test that a null context is rejected.
    /// </summary>
    [Fact]
    public async Task DownloadCommand_RunAsync_NullContext_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => DownloadCommand.RunAsync(null!, new FakeCliModelCatalog(), CancellationToken.None));
    }

    /// <summary>
    ///     Test that a null catalog is rejected.
    /// </summary>
    [Fact]
    public async Task DownloadCommand_RunAsync_NullCatalog_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["download", "model-1"]);
        await Assert.ThrowsAsync<ArgumentNullException>(() => DownloadCommand.RunAsync(context, null!, CancellationToken.None));
    }
}
