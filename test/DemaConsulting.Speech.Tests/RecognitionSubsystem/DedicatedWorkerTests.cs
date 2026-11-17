using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.RecognitionSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="DedicatedWorker"/>, exercising the cooperative-cancel-then-abandon
///     policy without any real recognition backend.
/// </summary>
public class DedicatedWorkerTests
{
    /// <summary>
    ///     Proves that a delegate which observes the cancellation token cooperatively and returns
    ///     promptly lets the returned task complete normally, without waiting out the abandon
    ///     timeout.
    /// </summary>
    [Fact]
    public async Task DedicatedWorker_Run_CooperativeCancellation_CompletesPromptly()
    {
        // Arrange: a worker with a generous abandon timeout that should never be reached, and a
        // delegate that waits on the token and returns the instant cancellation is requested
        var worker = new DedicatedWorker(abandonTimeout: TimeSpan.FromSeconds(30));
        using var cts = new CancellationTokenSource();
        using var started = new ManualResetEventSlim(false);

        var task = worker.RunAsync(
            token =>
            {
                started.Set();
                var signal = new ManualResetEventSlim(false);
                while (!token.IsCancellationRequested)
                {
                    signal.Wait(TimeSpan.FromMilliseconds(5));
                }
            },
            cts.Token);

        // Act: wait for the delegate to actually start, then cancel
        started.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        // Assert: the task completes normally (not canceled), well within the generous timeout
        await task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(task.IsCompletedSuccessfully);
    }

    /// <summary>
    ///     Proves that a delegate which never observes cancellation is abandoned once
    ///     <see cref="DedicatedWorker.AbandonTimeout"/> elapses, the returned task completes as
    ///     canceled, and the abandonment is reported through diagnostics at
    ///     <see cref="SpeechDiagnosticLevel.Warning"/>.
    /// </summary>
    [Fact]
    public async Task DedicatedWorker_Run_NonCooperativeDelegate_AbandonsAfterTimeoutAndReportsDiagnostics()
    {
        // Arrange: a worker with a near-zero abandon timeout and a diagnostics substitute, and a
        // delegate that blocks forever regardless of the supplied token
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var worker = new DedicatedWorker(
            abandonTimeout: TimeSpan.FromMilliseconds(1),
            diagnostics: diagnostics,
            diagnosticsCategory: "RecognitionSubsystem");
        using var cts = new CancellationTokenSource();
        using var neverSignaled = new ManualResetEventSlim(false);

        var task = worker.RunAsync(_ => neverSignaled.Wait(), cts.Token);

        // Act: cancel immediately so the delegate is given its (near-zero) abandon window
        await cts.CancelAsync();

        // Assert: the task completes as canceled rather than hanging forever
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Warning,
            "RecognitionSubsystem",
            Arg.Is<string>(message => message.Contains("abandon", StringComparison.OrdinalIgnoreCase)));

        // Cleanup: release the abandoned background thread so it can exit
        neverSignaled.Set();
    }

    /// <summary>
    ///     Proves that a delegate blocked inside the worker neither blocks the caller nor prevents
    ///     unrelated work from progressing, and that the returned task completes once the
    ///     delegate is released.
    /// </summary>
    [Fact]
    public async Task DedicatedWorker_Run_BlockedDelegate_DoesNotBlockCallerOrUnrelatedWork()
    {
        // Arrange: a delegate that signals it has started, then blocks until the test releases
        // it (the test token only terminates a hung test)
        var worker = new DedicatedWorker();
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);

        // Act: start the blocking delegate; the call must hand back its task without waiting
        var task = worker.RunAsync(
            _ =>
            {
                entered.Set();
                release.Wait(TestContext.Current.CancellationToken);
            },
            TestContext.Current.CancellationToken);

        // Assert: the delegate is blocked, the caller continues, and unrelated work completes
        Assert.True(entered.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        Assert.False(task.IsCompleted);
        var unrelated = await Task.Run(() => 42, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.Equal(42, unrelated);
        Assert.False(task.IsCompleted);

        // Act: release the delegate
        release.Set();

        // Assert: the task now completes normally
        await task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.True(task.IsCompletedSuccessfully);
    }
}
