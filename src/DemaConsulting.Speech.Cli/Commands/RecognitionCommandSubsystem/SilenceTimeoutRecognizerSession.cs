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

using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Cli.Commands.RecognitionCommandSubsystem;

/// <summary>
///     Observes an <see cref="ISpeechRecognizer"/>'s <see cref="ISpeechRecognizer.ResultReceived"/>
///     event and calls <see cref="ISpeechRecognizer.Stop"/>, then raises <see cref="TimedOut"/>,
///     when no result (partial or final) has arrived within a configured idle window - the
///     mic-mode implementation of <c>recognize --mic --silence-timeout &lt;seconds&gt;</c> (and its
///     companion <c>--start-timeout &lt;seconds&gt;</c> flag).
/// </summary>
/// <remarks>
///     <para>
///     This is a pure event-driven observer composed alongside a recognizer, not a decorator
///     around its lifecycle API: it never intercepts <see cref="ISpeechRecognizer.Start"/> or
///     <see cref="ISpeechRecognizer.Stop"/> calls made by its owner, and calls
///     <see cref="ISpeechRecognizer.Stop"/> itself only proactively, on timeout.
///     </para>
///     <para>
///     The idle timer is implemented with the injectable <see cref="System.TimeProvider"/>
///     abstraction (available in the BCL since .NET 8, requiring no new package reference) rather
///     than a hard-coded <see cref="Timer"/>/<see cref="Thread.Sleep(TimeSpan)"/>, so a unit test
///     can exercise the full idle/reset/timeout sequence deterministically with a fake
///     <see cref="System.TimeProvider"/> and no real wall-clock delay.
///     </para>
///     <para>
///     This session enforces two distinct, sequential idle windows. Before any
///     <see cref="ISpeechRecognizer.ResultReceived"/> event has arrived, the idle timer is armed
///     with the <c>startTimeout</c> value - a grace period for the user to begin speaking, which
///     is typically longer than the pause used to detect the end of an utterance. From the first
///     <see cref="ISpeechRecognizer.ResultReceived"/> event onward (partial or final), the timer
///     is re-armed with <c>idleTimeout</c> (the <c>--silence-timeout</c> value) on every
///     subsequent event, which is the simplest correct semantics for "reset on every event". No
///     extra "first result seen" flag is needed for this phase transition: construction and
///     <see cref="OnResultReceived"/> are already distinct call sites, so arming with
///     <c>startTimeout</c> once at construction and unconditionally re-arming with
///     <c>idleTimeout</c> on every <see cref="OnResultReceived"/> call naturally implements the
///     two-phase contract.
///     </para>
/// </remarks>
internal sealed class SilenceTimeoutRecognizerSession : IDisposable
{
    /// <summary>The recognizer this session observes and, on timeout, stops.</summary>
    private readonly ISpeechRecognizer _recognizer;

    /// <summary>The idle window; re-armed on every <see cref="ISpeechRecognizer.ResultReceived"/> event.</summary>
    private readonly TimeSpan _idleTimeout;

    /// <summary>The single-shot idle timer.</summary>
    private readonly ITimer _timer;

    /// <summary>
    ///     Serializes <see cref="OnResultReceived"/>, <see cref="OnIdle"/>, and <see cref="Dispose"/>
    ///     against one another, so a timer/result callback racing a concurrent
    ///     <see cref="Dispose"/> call always either completes entirely before disposal starts or
    ///     observes <see cref="_isDisposed"/> already set and returns immediately - it never reads
    ///     a torn state or touches the timer after it has been disposed.
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    ///     Guards against a timer callback racing a concurrent <see cref="Dispose"/> call: once
    ///     set, the timer callback (which runs on a thread-pool thread independent of the
    ///     constructing/disposing thread) must not call <see cref="ISpeechRecognizer.Stop"/> or
    ///     raise <see cref="TimedOut"/> once <see cref="Dispose"/> has already started and
    ///     observed the callback hasn't fired yet. Always read/written while holding
    ///     <see cref="_gate"/>; see <see cref="OnIdle"/> for why <see cref="_gate"/> is released
    ///     again before that call and raise actually happen.
    /// </summary>
    private bool _isDisposed;

    /// <summary>
    ///     Signaled whenever no <see cref="OnIdle"/> invocation is currently between its
    ///     <see cref="_gate"/>-protected disposed-check and the completion of its
    ///     <see cref="ISpeechRecognizer.Stop"/> call and <see cref="TimedOut"/> raise. Starts
    ///     signaled (no callback in flight). <see cref="Dispose"/> waits on this - outside
    ///     <see cref="_gate"/> - so it still happens-after any in-flight
    ///     <see cref="ISpeechRecognizer.Stop"/> call and <see cref="TimedOut"/> raise, even though
    ///     <see cref="OnIdle"/> no longer holds <see cref="_gate"/> while making them (see
    ///     <see cref="OnIdle"/> for why holding <see cref="_gate"/> across those calls would
    ///     deadlock). Without this, a caller could observe <see cref="Dispose"/> return and then
    ///     tear down state a <see cref="TimedOut"/> handler still in flight depends on (for
    ///     example disposing a synchronization primitive a handler closure calls back into, as
    ///     <c>RecognizeCommand</c> does with its shared <c>stopSignal</c>).
    /// </summary>
    /// <remarks>
    ///     This assumes a <see cref="TimedOut"/> subscriber never calls <see cref="Dispose"/>
    ///     synchronously from within its own handler - <see cref="Dispose"/> would deadlock
    ///     waiting on this event in that case, since only that same (blocked) thread's own
    ///     <see cref="OnIdle"/> invocation could ever signal it. This session's sole caller,
    ///     <c>RecognizeCommand</c>, only ever sets a flag from its <see cref="TimedOut"/> handler
    ///     and calls <see cref="Dispose"/> later, separately, after observing that flag on its own
    ///     thread, so this is not a real constraint in practice today.
    /// </remarks>
    private readonly ManualResetEventSlim _idleCallbackDone = new(initialState: true);

    /// <summary>
    ///     Initializes a new instance of the <see cref="SilenceTimeoutRecognizerSession"/> class,
    ///     arming its idle timer immediately.
    /// </summary>
    /// <param name="recognizer">The recognizer to observe and, on timeout, stop. Must not be null.</param>
    /// <param name="idleTimeout">
    ///     The idle window after which, with no <see cref="ISpeechRecognizer.ResultReceived"/>
    ///     event, this session calls <see cref="ISpeechRecognizer.Stop"/>. Used to re-arm the
    ///     timer on every <see cref="ISpeechRecognizer.ResultReceived"/> event, and as the
    ///     initial arming value when <paramref name="startTimeout"/> is <see langword="null"/>.
    ///     Must be greater than <see cref="TimeSpan.Zero"/>.
    /// </param>
    /// <param name="timeProvider">
    ///     The time source to create the idle timer from, or <see langword="null"/> to use
    ///     <see cref="TimeProvider.System"/>, mirroring <c>AudioDeviceFactory</c>'s own "null seam
    ///     parameter defaults to the real backend" convention.
    /// </param>
    /// <param name="startTimeout">
    ///     The idle window used only for the initial timer arming, before any
    ///     <see cref="ISpeechRecognizer.ResultReceived"/> event has arrived - a grace period for
    ///     the user to begin speaking, which is typically longer than the pause used to detect
    ///     the end of an utterance. Defaults to <paramref name="idleTimeout"/> when
    ///     <see langword="null"/>. Callers such as <c>RecognizeCommand</c>/<c>AskCommand</c>
    ///     instead resolve their own independent default before constructing this session, so
    ///     this fallback is exercised only by callers that genuinely want one flag to imply the
    ///     other. Must be greater than <see cref="TimeSpan.Zero"/> when supplied.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="recognizer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="idleTimeout"/> is not greater than <see cref="TimeSpan.Zero"/>,
    ///     or when <paramref name="startTimeout"/> has a value that is not greater than
    ///     <see cref="TimeSpan.Zero"/>.
    /// </exception>
    public SilenceTimeoutRecognizerSession(
        ISpeechRecognizer recognizer,
        TimeSpan idleTimeout,
        TimeProvider? timeProvider = null,
        TimeSpan? startTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(recognizer);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleTimeout, TimeSpan.Zero);
        if (startTimeout.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(startTimeout.Value, TimeSpan.Zero);
        }

        _recognizer = recognizer;
        _idleTimeout = idleTimeout;

        var provider = timeProvider ?? TimeProvider.System;
        _timer = provider.CreateTimer(OnIdle, null, startTimeout ?? idleTimeout, Timeout.InfiniteTimeSpan);

        _recognizer.ResultReceived += OnResultReceived;
    }

    /// <summary>
    ///     Raised after this session has already called <see cref="ISpeechRecognizer.Stop"/>
    ///     because no result arrived within the idle window.
    /// </summary>
    public event EventHandler? TimedOut;

    /// <summary>
    ///     Re-arms the idle timer on every result (partial or final), per this session's "reset
    ///     on every event" contract.
    /// </summary>
    private void OnResultReceived(object? sender, SpeechRecognitionEvent e)
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            // Holding _gate for the whole check-then-act guarantees Dispose cannot have disposed
            // _timer between the _isDisposed check above and this call: Dispose only disposes
            // _timer after it has both set _isDisposed and released _gate (see Dispose below), so
            // observing _isDisposed == false here under the lock proves _timer is still live.
            _timer.Change(_idleTimeout, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    ///     Runs when the idle timer fires with no reset since it was last armed: stops the
    ///     recognizer, then raises <see cref="TimedOut"/>.
    /// </summary>
    private void OnIdle(object? state)
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            // Reset while still holding _gate: this is the only place _idleCallbackDone is
            // reset, and _isDisposed being false here (still checked under _gate) proves Dispose
            // has not yet started waiting on it, so there is no race with the Wait() in Dispose.
            _idleCallbackDone.Reset();
        }

        try
        {
            // Stop() and the TimedOut raise deliberately happen without holding _gate. Stop()
            // drains already-captured audio and can block waiting for the recognizer's
            // background decode thread to finish, and that thread may itself raise
            // ResultReceived while draining - which needs _gate to reset the idle timer in
            // OnResultReceived. Holding _gate across Stop() here would deadlock the two threads
            // against each other (this thread blocked inside Stop() waiting for the decode
            // thread, the decode thread blocked waiting to enter _gate). Releasing _gate first
            // means a concurrent Dispose can now set _isDisposed and unsubscribe before Stop()
            // returns; that is harmless because the recognizer is owned by this session's
            // caller, not by the session itself, so calling Stop() (and raising TimedOut) after
            // this session's own bookkeeping has been torn down is still safe. Dispose still
            // waits for this call to finish (via _idleCallbackDone) before returning, so callers
            // never observe Dispose complete while a TimedOut raise is still in flight.
            _recognizer.Stop();
            TimedOut?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _idleCallbackDone.Set();
        }
    }

    /// <summary>
    ///     Disposes the idle timer and unsubscribes from <see cref="ISpeechRecognizer.ResultReceived"/>.
    ///     Idempotent: a second call does nothing.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _recognizer.ResultReceived -= OnResultReceived;
        }

        // _timer.Dispose() runs outside _gate deliberately: some TimeProvider/ITimer
        // implementations block a Dispose() call until any currently in-flight callback
        // invocation has finished. Since OnIdle also acquires _gate, disposing the timer while
        // still holding _gate here could deadlock (this thread blocked inside _timer.Dispose()
        // waiting for OnIdle to return, while OnIdle is blocked waiting to acquire the very lock
        // this thread holds). Releasing _gate first (having already set _isDisposed under it)
        // still guarantees no new call to OnResultReceived/OnIdle acts on this session, since
        // both re-check _isDisposed immediately after acquiring _gate.
        //
        // Wait for _idleCallbackDone before disposing the timer: an OnIdle invocation already
        // past the _isDisposed check above (and therefore already committed to calling Stop()
        // and raising TimedOut) is not tracked by _gate at all once it releases it, so without
        // this wait, Dispose could return - and a caller could tear down state a still-in-flight
        // TimedOut handler depends on - before that call actually happens (see _idleCallbackDone
        // for the one assumption this relies on: no TimedOut subscriber calls Dispose() itself).
        _idleCallbackDone.Wait();
        _timer.Dispose();
        _idleCallbackDone.Dispose();
    }
}
