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
///     mic-mode implementation of <c>recognize --mic --silence-timeout &lt;seconds&gt;</c>.
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
///     The single-shot timer is re-armed explicitly (<c>Change(idleTimeout,
///     Timeout.InfiniteTimeSpan)</c>) on every <see cref="ISpeechRecognizer.ResultReceived"/>
///     event, rather than relying on a periodic period, which is the simplest correct semantics
///     for "reset the idle window on every event".
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
    ///     raise <see cref="TimedOut"/> on an already-torn-down session. Always read/written while
    ///     holding <see cref="_gate"/>.
    /// </summary>
    private bool _isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SilenceTimeoutRecognizerSession"/> class,
    ///     arming its idle timer immediately.
    /// </summary>
    /// <param name="recognizer">The recognizer to observe and, on timeout, stop. Must not be null.</param>
    /// <param name="idleTimeout">
    ///     The idle window after which, with no <see cref="ISpeechRecognizer.ResultReceived"/>
    ///     event, this session calls <see cref="ISpeechRecognizer.Stop"/>. Must be greater than
    ///     <see cref="TimeSpan.Zero"/>.
    /// </param>
    /// <param name="timeProvider">
    ///     The time source to create the idle timer from, or <see langword="null"/> to use
    ///     <see cref="TimeProvider.System"/>, mirroring <c>AudioDeviceFactory</c>'s own "null seam
    ///     parameter defaults to the real backend" convention.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="recognizer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="idleTimeout"/> is not greater than <see cref="TimeSpan.Zero"/>.</exception>
    public SilenceTimeoutRecognizerSession(
        ISpeechRecognizer recognizer,
        TimeSpan idleTimeout,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(recognizer);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleTimeout, TimeSpan.Zero);

        _recognizer = recognizer;
        _idleTimeout = idleTimeout;

        var provider = timeProvider ?? TimeProvider.System;
        _timer = provider.CreateTimer(OnIdle, null, idleTimeout, Timeout.InfiniteTimeSpan);

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

            // Stop() and the TimedOut raise both happen while still holding _gate, for the same
            // reason as OnResultReceived above: a concurrent Dispose cannot have torn down
            // _recognizer's subscription or _timer while this thread holds the lock, and lock is
            // reentrant on this thread, so a TimedOut handler that itself calls Dispose() does
            // not deadlock.
            _recognizer.Stop();
            TimedOut?.Invoke(this, EventArgs.Empty);
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
        _timer.Dispose();
    }
}
