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

using System.Runtime.CompilerServices;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Cli.Commands.RecognitionCommandSubsystem;

/// <summary>
///     Wraps an <see cref="IRecognitionSession"/>'s <see cref="IRecognitionSession.GetResultsAsync"/>
///     stream, calling <see cref="IRecognitionSession.StopAsync"/>, then raising
///     <see cref="TimedOut"/>, when no result (partial or final) has arrived within a configured
///     idle window - the mic-mode implementation of <c>recognize --mic --silence-timeout
///     &lt;seconds&gt;</c> (and its companion <c>--start-timeout &lt;seconds&gt;</c> flag).
/// </summary>
/// <remarks>
///     <para>
///     <b>Redesign from the old event-observer shape.</b> Before the Engine/Session redesign,
///     this type observed <c>ISpeechRecognizer.ResultReceived</c> - an event raised from the
///     recognizer's own background decode thread - and had to serialize that callback against a
///     concurrent <see cref="IDisposable.Dispose"/> call with a hand-rolled gate/wait-handle pair,
///     because a timer callback and <c>Dispose</c> could race on two different threads with no
///     other synchronization available. The new <see cref="IRecognitionSession.GetResultsAsync"/>
///     contract is <i>single-consumer</i> and fully <c>async</c>: there is no longer a separate
///     background thread raising events into this type at arbitrary times, so there is nothing
///     left to race. This type is instead implemented as a stateless <see cref="IAsyncEnumerable{T}"/>
///     decorator: <see cref="GetResultsAsync"/> is itself an async-iterator method whose entire
///     idle-timeout bookkeeping lives on its own call stack (a local <c>await using</c> enumerator
///     and a per-iteration <see cref="Task.Delay(TimeSpan,TimeProvider,CancellationToken)"/>), so
///     it needs no locks, no wait handles, and - since it owns no state beyond one call's local
///     variables - no <see cref="IAsyncDisposable"/>/<see cref="IDisposable"/> of its own either;
///     an abandoned enumeration (for example a caller that stops awaiting <c>MoveNextAsync</c>
///     partway through) is cleaned up the same way any other async-iterator method's locals are,
///     through the compiler-generated <c>await using</c> around the inner enumerator.
///     </para>
///     <para>
///     Each loop iteration races the next <see cref="IRecognitionSession.GetResultsAsync"/> item
///     against a fresh <see cref="Task.Delay(TimeSpan,TimeProvider,CancellationToken)"/> idle
///     timer via <see cref="Task.WhenAny(Task,Task)"/>. The idle timer always wins a race against
///     a result that has not yet arrived, since <see cref="Task.WhenAny(Task,Task)"/> is
///     non-blocking and only ever observes tasks that are already pending; there is no window
///     where both could appear "ready" simultaneously in a way that silently drops a genuine
///     result - a result that arrives first simply completes the inner task first, exactly as a
///     single <c>await foreach</c> over the undecorated session would observe it.
///     </para>
///     <para>
///     This session enforces two distinct, sequential idle windows, identically to the prior
///     design: before any result has arrived, the idle timer is armed with the
///     <c>startTimeout</c> constructor parameter's value - a grace period for the user to begin
///     speaking, typically longer than the pause used to detect the end of an utterance. From
///     the first yielded result onward (partial or final),
///     the timer is re-armed with <c>idleTimeout</c> (the <c>--silence-timeout</c> value) for
///     every subsequent iteration.
///     </para>
///     <para>
///     On timeout, this type calls <see cref="IRecognitionSession.StopAsync"/> - which, per its
///     own contract, drains any already-captured-but-not-yet-decoded audio and completes the
///     session's result stream - then raises <see cref="TimedOut"/>, then continues draining
///     <see cref="IRecognitionSession.GetResultsAsync"/> itself so that any trailing result
///     produced by that drain is still yielded to this type's own caller before the enumeration
///     ends, exactly as it would have been without this decorator in place.
///     </para>
/// </remarks>
internal sealed class SilenceTimeoutRecognizerSession
{
    /// <summary>The session this type wraps and, on timeout, stops.</summary>
    private readonly IRecognitionSession _session;

    /// <summary>The idle window; re-armed after every yielded result.</summary>
    private readonly TimeSpan _idleTimeout;

    /// <summary>The idle window used only for the initial arming, before any result has arrived.</summary>
    private readonly TimeSpan _startTimeout;

    /// <summary>The time source the idle timer is created from.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SilenceTimeoutRecognizerSession"/> class.
    /// </summary>
    /// <param name="session">The session to wrap and, on timeout, stop. Must not be null.</param>
    /// <param name="idleTimeout">
    ///     The idle window after which, with no result yielded, this type calls
    ///     <see cref="IRecognitionSession.StopAsync"/>. Used to re-arm the timer after every
    ///     yielded result, and as the initial arming value when <paramref name="startTimeout"/> is
    ///     <see langword="null"/>. Must be greater than <see cref="TimeSpan.Zero"/>.
    /// </param>
    /// <param name="timeProvider">
    ///     The time source to race the idle timer against, or <see langword="null"/> to use
    ///     <see cref="TimeProvider.System"/>, mirroring <c>AudioDeviceFactory</c>'s own "null seam
    ///     parameter defaults to the real backend" convention.
    /// </param>
    /// <param name="startTimeout">
    ///     The idle window used only for the initial timer arming, before any result has arrived -
    ///     a grace period for the user to begin speaking, which is typically longer than the pause
    ///     used to detect the end of an utterance. Defaults to <paramref name="idleTimeout"/> when
    ///     <see langword="null"/>. Callers such as <c>RecognizeCommand</c>/<c>AskCommand</c>
    ///     instead resolve their own independent default before constructing this session, so
    ///     this fallback is exercised only by callers that genuinely want one flag to imply the
    ///     other. Must be greater than <see cref="TimeSpan.Zero"/> when supplied.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="idleTimeout"/> is not greater than <see cref="TimeSpan.Zero"/>,
    ///     or when <paramref name="startTimeout"/> has a value that is not greater than
    ///     <see cref="TimeSpan.Zero"/>.
    /// </exception>
    public SilenceTimeoutRecognizerSession(
        IRecognitionSession session,
        TimeSpan idleTimeout,
        TimeProvider? timeProvider = null,
        TimeSpan? startTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleTimeout, TimeSpan.Zero);
        if (startTimeout.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(startTimeout.Value, TimeSpan.Zero);
        }

        _session = session;
        _idleTimeout = idleTimeout;
        _startTimeout = startTimeout ?? idleTimeout;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    ///     Raised after this session has already called <see cref="IRecognitionSession.StopAsync"/>
    ///     because no result arrived within the idle window.
    /// </summary>
    public event EventHandler? TimedOut;

    /// <summary>
    ///     Streams every result from the wrapped session, calling
    ///     <see cref="IRecognitionSession.StopAsync"/> and raising <see cref="TimedOut"/> once, the
    ///     first time the configured idle window elapses with no result yielded.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A token that ends only this enumeration when cancelled - the wrapped session itself
    ///     keeps running, mirroring <see cref="IRecognitionSession.GetResultsAsync"/>'s own
    ///     contract.
    /// </param>
    /// <returns>An asynchronous sequence of recognition events.</returns>
    public async IAsyncEnumerable<SpeechRecognitionEvent> GetResultsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var timeout = _startTimeout;
        var timedOut = false;

        await using var enumerator = _session.GetResultsAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            var moveNextTask = enumerator.MoveNextAsync().AsTask();
            var delayTask = Task.Delay(timeout, _timeProvider, cancellationToken);
            var winner = await Task.WhenAny(moveNextTask, delayTask).ConfigureAwait(false);

            // A delayTask that "wins" because cancellationToken was cancelled (rather than
            // because the idle window genuinely elapsed) is not a timeout: it is
            // IsCompletedSuccessfully only when the idle window itself ran to completion, while a
            // cancellation instead leaves it Canceled. Treating a cancellation-triggered delayTask
            // as a timeout would wrongly call StopAsync here, racing the wrapped session's result
            // stream completion against moveNextTask's own cancellation and sometimes swallowing
            // the OperationCanceledException this enumeration's caller is entitled to observe.
            if (winner == delayTask && delayTask.IsCompletedSuccessfully && !timedOut)
            {
                // The idle window elapsed with no result yielded since the previous iteration (or
                // since this method started, for the first iteration). Stop the session - which
                // completes its result stream - then raise TimedOut, then keep draining below so
                // any trailing result the stop itself produced is still yielded to our own
                // caller, exactly as an undecorated foreach over the session would observe it.
                timedOut = true;
                await _session.StopAsync(CancellationToken.None).ConfigureAwait(false);
                TimedOut?.Invoke(this, EventArgs.Empty);
            }

            // Propagate cancellation (either delayTask's own cancellation, or a fault surfaced
            // through moveNextTask) the same way the standard compiler-generated "await foreach"
            // would: by awaiting the task that is actually ready, letting its own exception (if
            // any) propagate naturally.
            if (!await moveNextTask.ConfigureAwait(false))
            {
                yield break;
            }

            yield return enumerator.Current;
            timeout = _idleTimeout;
        }
    }
}
