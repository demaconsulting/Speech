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

        // Assert: the call completes (cancelled) well before the 10-second abandon timeout
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    /// <summary>
    ///     Proves that a delegate which never observes cancellation is abandoned once the
    ///     (short, injected) abandon timeout elapses, that the returned task completes as
    ///     cancelled, and that a warning is reported through diagnostics.
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

        // Assert: the call is abandoned and reported as cancelled, rather than hanging
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Warning,
            "SynthesisSubsystem",
            Arg.Is<string>(message => message.Contains("abandoned", StringComparison.Ordinal)));

        // Release the abandoned worker thread so it does not outlive the test.
        release.Release();
    }

    /// <summary>
    ///     Proves that <see cref="DedicatedWorker.Run{T}"/> runs its delegate on a dedicated
    ///     long-running thread rather than a pooled thread-pool thread, by observing
    ///     <see cref="Thread.IsThreadPoolThread"/> from inside the delegate.
    /// </summary>
    [Fact]
    public async Task DedicatedWorker_Run_UsesLongRunningTaskCreationOption()
    {
        // Act
        var isThreadPoolThread = await DedicatedWorker.Run(
            _ => Thread.CurrentThread.IsThreadPoolThread,
            CancellationToken.None,
            null,
            "SynthesisSubsystem");

        // Assert
        Assert.False(isThreadPoolThread);
    }
}
