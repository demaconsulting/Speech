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

namespace DemaConsulting.Speech.Demo.Tests;

/// <summary>
///     Unit tests for <see cref="App"/>'s composition and teardown seams, exercised without a
///     running desktop lifetime.
/// </summary>
public sealed class AppTests
{
    /// <summary>
    ///     Builds launch options whose model store is rooted in a fresh directory under the test
    ///     output folder, so no test touches a developer's real installed-model store.
    /// </summary>
    /// <returns>Launch options with an isolated models directory.</returns>
    private static AppLaunchOptions IsolatedLaunchOptions() => AppLaunchOptions.Parse(
        ["--models-dir", Path.Join(AppContext.BaseDirectory, "AppTests", Guid.NewGuid().ToString("N"))],
        _ => null);

    /// <summary>
    ///     Test that <c>App.Compose</c> produces the shell with all four panels, opening on the first.
    /// </summary>
    [Fact]
    public void App_Compose_IsolatedLaunchOptions_ComposesShellWithAllPanels()
    {
        // Act
        var viewModel = App.Compose(IsolatedLaunchOptions(), out var catalogService);
        using var disposable = catalogService;

        // Assert
        Assert.Equal(4, viewModel.Panels.Count);
        Assert.Same(viewModel.Panels[0], viewModel.SelectedPanel);
        Assert.Same(viewModel.DeviceSelection, viewModel.Panels[0].Content);
        Assert.Same(viewModel.ModelCatalog, viewModel.Panels[1].Content);
        Assert.Same(viewModel.Synthesis, viewModel.Panels[2].Content);
        Assert.Same(viewModel.Recognition, viewModel.Panels[3].Content);
    }

    /// <summary>
    ///     Test that the composed catalog registers every model family the demo offers, so the catalog
    ///     panel lists a model from each family.
    /// </summary>
    [Fact]
    public void App_Compose_IsolatedLaunchOptions_CatalogListsEveryRegisteredModel()
    {
        // Act
        var viewModel = App.Compose(IsolatedLaunchOptions(), out var catalogService);
        using var disposable = catalogService;

        // Assert: the panel lists a model from each documented family, by literal identifier
        var listed = viewModel.ModelCatalog.Models.Select(m => m.Id).ToList();
        foreach (var expected in ExpectedModelIds)
        {
            Assert.Contains(expected, listed);
        }

        Assert.Contains(listed, id => id.StartsWith(ExpectedChatterboxIdPrefix, StringComparison.Ordinal));
        Assert.False(viewModel.ModelCatalog.IsCatalogEmpty);
    }

    /// <summary>
    ///     Literal identifiers of the Sherpa, Kokoro and Nemotron models the demo documents as
    ///     offered (stated independently of the library's own constants).
    /// </summary>
    internal static readonly string[] ExpectedModelIds =
    [
        "streaming-zipformer-en-2023-06-26",
        "nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25",
        "vits-piper-en_US-libritts_r-medium",
        "kokoro-int8-en-v0_19",
        "kokoro-onnx-v1_0-en",
        "nemotron-3.5-asr-streaming-0.6b-onnx-int4",
    ];

    /// <summary>
    ///     Literal identifier prefix of the Chatterbox Turbo model (variants append a suffix).
    /// </summary>
    internal const string ExpectedChatterboxIdPrefix = "chatterbox-turbo-onnx-en";

    /// <summary>
    ///     Test that composing with mirror launch options pre-populates the catalog panel's
    ///     mirror settings from them, so the launch-time and in-app configuration agree.
    /// </summary>
    [Fact]
    public void App_Compose_MirrorLaunchOptions_PrePopulatesCatalogPanelMirrorSettings()
    {
        // Arrange
        var options = AppLaunchOptions.Parse(
            [
                "--models-dir", Path.Join(AppContext.BaseDirectory, "AppTests", Guid.NewGuid().ToString("N")),
                "--mirror-url", "https://mirror.example.com",
                "--mirror-bearer-token", "token-value",
            ],
            _ => null);

        // Act
        var viewModel = App.Compose(options, out var catalogService);
        using var disposable = catalogService;

        // Assert
        Assert.Equal("https://mirror.example.com", viewModel.ModelCatalog.MirrorUrl);
        Assert.Equal("token-value", viewModel.ModelCatalog.MirrorBearerToken);
    }

    /// <summary>
    ///     Test that composing with no launch options (the design-time case) still succeeds.
    /// </summary>
    [Fact]
    public void App_Compose_NullLaunchOptions_ComposesShell()
    {
        // Act
        var exception = Record.Exception(() =>
        {
            var viewModel = App.Compose(null, out var catalogService);
            catalogService.Dispose();
            Assert.Equal(4, viewModel.Panels.Count);
        });

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Test that the first shutdown request is deferred, cleanup runs to completion, and the
    ///     lifetime's own shutdown is then called exactly once.
    /// </summary>
    [Fact]
    public async Task App_HandleShutdownRequested_FirstRequest_DefersThenResumesShutdownOnce()
    {
        // Arrange
        var viewModel = App.Compose(IsolatedLaunchOptions(), out var catalogService);
        using var disposable = catalogService;
        var app = new App();
        var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeCalls = 0;

        // Act
        var deferred = app.HandleShutdownRequested(() => { resumeCalls++; resumed.TrySetResult(); }, viewModel);
        await resumed.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(deferred);
        Assert.NotNull(app.ShutdownTask);
        await app.ShutdownTask;
        Assert.Equal(1, resumeCalls);
    }

    /// <summary>
    ///     Test that a repeated request is never answered by starting a second cleanup, is
    ///     deferred while cleanup is held pending, and is let through once cleanup has completed
    ///     (the genuine second request raised by the lifetime's own shutdown).
    /// </summary>
    [Fact]
    public async Task App_HandleShutdownRequested_RepeatedRequest_StartsNoSecondCleanup()
    {
        // Arrange
        var viewModel = App.Compose(IsolatedLaunchOptions(), out var catalogService);
        using var disposable = catalogService;
        var app = new App();
        var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeCalls = 0;
        void Resume()
        {
            resumeCalls++;
            resumed.TrySetResult();
        }

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act: the first request starts cleanup, which is held pending by the gate
        var first = app.HandleShutdownRequested(Resume, viewModel, () => gate.Task);
        var firstTask = app.ShutdownTask!;
        var whilePending = app.HandleShutdownRequested(Resume, viewModel, () => gate.Task);
        var stillPending = !firstTask.IsCompleted;
        var resumedWhilePending = resumeCalls;
        gate.SetResult();
        await resumed.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await firstTask;
        var afterCleanup = app.HandleShutdownRequested(Resume, viewModel);

        // Assert: deferred while cleanup is pending, never resumed early, no second cleanup,
        // and let through once cleanup has completed
        Assert.True(first);
        Assert.True(stillPending);
        Assert.True(whilePending);
        Assert.Equal(0, resumedWhilePending);
        Assert.False(afterCleanup);
        Assert.Same(firstTask, app.ShutdownTask);
        Assert.Equal(1, resumeCalls);
    }
    /// <summary>
    ///     Test that the shutdown task is absent until a shutdown has been requested.
    /// </summary>
    [Fact]
    public void App_ShutdownTask_BeforeAnyRequest_IsNull()
    {
        Assert.Null(new App().ShutdownTask);
    }

    /// <summary>
    ///     Test that teardown runs every step in order when none fails.
    /// </summary>
    [Fact]
    public async Task App_RunTeardownAsync_AllStepsSucceed_RunsEveryStepInOrder()
    {
        // Arrange
        var order = new List<int>();

        // Act
        await App.RunTeardownAsync(
            () => { order.Add(1); return ValueTask.CompletedTask; },
            () => { order.Add(2); return ValueTask.CompletedTask; },
            () => { order.Add(3); return ValueTask.CompletedTask; });

        // Assert
        Assert.Equal([1, 2, 3], order);
    }

    /// <summary>
    ///     Test that a step that throws synchronously, and one that faults asynchronously, are each
    ///     contained so every later step still runs.
    /// </summary>
    [Fact]
    public async Task App_RunTeardownAsync_StepsFail_ContainsEachFailureAndRunsRemainingSteps()
    {
        // Arrange
        var order = new List<int>();

        // Act
        var exception = await Record.ExceptionAsync(() => App.RunTeardownAsync(
            () => { order.Add(1); throw new InvalidOperationException("first"); },
            async () => { order.Add(2); await Task.Yield(); throw new InvalidOperationException("second"); },
            () => { order.Add(3); return ValueTask.CompletedTask; }));

        // Assert
        Assert.Null(exception);
        Assert.Equal([1, 2, 3], order);
    }

    /// <summary>
    ///     Test that a build that throws releases the owner it was given and rethrows, so a
    ///     composition failure cannot leak the catalog service the caller never receives.
    /// </summary>
    [Fact]
    public void App_BuildOrDispose_BuildThrows_DisposesOwnerAndRethrows()
    {
        // Arrange
        var owner = new CountingDisposable();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => App.BuildOrDispose<object>(owner, () => throw new InvalidOperationException("build")));

        // Assert
        Assert.Equal("build", exception.Message);
        Assert.Equal(1, owner.DisposeCount);
    }

    /// <summary>
    ///     Test that a successful build returns its result and leaves the owner open.
    /// </summary>
    [Fact]
    public void App_BuildOrDispose_BuildSucceeds_ReturnsResultAndLeavesOwnerOpen()
    {
        // Arrange
        var owner = new CountingDisposable();
        var built = new object();

        // Act
        var result = App.BuildOrDispose(owner, () => built);

        // Assert
        Assert.Same(built, result);
        Assert.Equal(0, owner.DisposeCount);
    }

    /// <summary>
    ///     A disposable that counts how many times it was disposed.
    /// </summary>
    private sealed class CountingDisposable : IDisposable
    {
        /// <summary>Gets the number of times <see cref="Dispose"/> was called.</summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        public void Dispose() => DisposeCount++;
    }
}
