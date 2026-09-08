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
    ///     Guards against a timer callback racing a concurrent <see cref="Dispose"/> call: once
    ///     set, the timer callback (which runs on a thread-pool thread independent of the
    ///     constructing/disposing thread) must not call <see cref="ISpeechRecognizer.Stop"/> or
    ///     raise <see cref="TimedOut"/> on an already-torn-down session.
    /// </summary>
    private volatile bool _isDisposed;

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
        if (_isDisposed)
        {
            return;
        }

        _timer.Change(_idleTimeout, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    ///     Runs when the idle timer fires with no reset since it was last armed: stops the
    ///     recognizer, then raises <see cref="TimedOut"/>.
    /// </summary>
    private void OnIdle(object? state)
    {
        if (_isDisposed)
        {
            return;
        }

        _recognizer.Stop();
        TimedOut?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Disposes the idle timer and unsubscribes from <see cref="ISpeechRecognizer.ResultReceived"/>.
    ///     Idempotent: a second call does nothing.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        _recognizer.ResultReceived -= OnResultReceived;
        _timer.Dispose();
    }
}
