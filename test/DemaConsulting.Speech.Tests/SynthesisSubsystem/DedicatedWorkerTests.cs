using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.SynthesisSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="DedicatedWorker"/>, proving the cooperative-cancel-then-abandon
///     policy deterministically, without relying on the real two-second default timeout.
/// </summary>
public sealed class DedicatedWorkerTests
{
    /// <summary>
    ///     Proves that a delegate honoring cancellation completes promptly, without waiting for
    ///     the abandon timeout to elapse.
    /// </summary>
    [Fact]
    public async Task DedicatedWorker_Run_CooperativeCancellation_CompletesPromptly()
    {
        // Arrange: a delegate that cooperatively observes cancellation and stops immediately
        using var cts = new CancellationTokenSource();

        // Act
        var task = DedicatedWorker.Run(
            token =>
            {
                while (!token.IsCancellationRequested)
                {
                    SpinWait.SpinUntil(() => token.IsCancellationRequested, TimeSpan.FromMilliseconds(5));
                }

                token.ThrowIfCancellationRequested();
                return 0;
            },
            cts.Token,
            null,
            "SynthesisSubsystem",
            TimeSpan.FromSeconds(10));
        await cts.CancelAsync();

        // Assert: the call completes (canceled) well before the 10-second abandon timeout
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    /// <summary>
    ///     Proves that a delegate which never observes cancellation is abandoned once the
    ///     (short, injected) abandon timeout elapses, that the returned task completes as
    ///     canceled, and that a warning is reported through diagnostics.
    /// </summary>
    [Fact]
    public async Task DedicatedWorker_Run_NonCooperativeDelegate_AbandonsAfterTimeoutAndReportsDiagnostics()
    {
        // Arrange: a delegate that never checks its token, and a short injected abandon timeout
        using var cts = new CancellationTokenSource();
        using var release = new SemaphoreSlim(0, 1);
        var diagnostics = Substitute.For<ISpeechDiagnostics>();

        // Act
        var task = DedicatedWorker.Run(
            _ =>
            {
                release.Wait(TestContext.Current.CancellationToken);
                return 0;
            },
            cts.Token,
            diagnostics,
            "SynthesisSubsystem",
            TimeSpan.FromMilliseconds(50));
        await cts.CancelAsync();

        // Assert: the call is abandoned and reported as canceled, rather than hanging
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Warning,
            "SynthesisSubsystem",
            Arg.Is<string>(message => message.Contains("abandoned", StringComparison.Ordinal)));

        // Release the abandoned worker thread so it does not outlive the test.
        release.Release();
    }

    /// <summary>
    ///     Proves that a delegate blocked inside <see cref="DedicatedWorker.Run{T}"/> neither
    ///     blocks the caller nor prevents unrelated work from progressing, and that the returned
    ///     task completes with the delegate's result once it is released.
    /// </summary>
    [Fact(Timeout = 60000)]
    public async Task DedicatedWorker_Run_BlockedDelegate_DoesNotBlockCallerOrUnrelatedWork()
    {
        // Arrange: a delegate that signals it has started, then blocks until the test releases it;
        // the delegate's wait is bounded so a regression fails the test instead of hanging it
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        try
        {
            // Act: start the blocking delegate; the call must hand back its task without waiting
            var task = DedicatedWorker.Run(
                _ =>
                {
                    entered.Set();
                    release.Wait(TimeSpan.FromSeconds(15));
                    return 7;
                },
                CancellationToken.None,
                null,
                "SynthesisSubsystem");

            // Assert: the delegate is blocked, the caller continues, and unrelated work completes
            Assert.True(entered.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
            Assert.False(task.IsCompleted);
            var unrelated = await Task.Run(() => 42, TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.Equal(42, unrelated);
            Assert.False(task.IsCompleted);

            // Act: release the delegate
            release.Set();

            // Assert: the task completes with the delegate's result
            Assert.Equal(7, await task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        }
        finally
        {
            release.Set();
        }
    }
}
