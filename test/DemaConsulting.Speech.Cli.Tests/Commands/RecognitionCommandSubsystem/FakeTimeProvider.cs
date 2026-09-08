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

namespace DemaConsulting.Speech.Cli.Tests.Commands.RecognitionCommandSubsystem;

/// <summary>
///     Minimal, deterministic test-only <see cref="TimeProvider"/> subclass whose single created
///     timer's callback a test can invoke synchronously, letting
///     <c>SilenceTimeoutRecognizerSessionTests</c> exercise the full idle/reset/timeout sequence
///     with no real wall-clock delay and no <c>Microsoft.Extensions.Time.Testing</c> package
///     reference.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    /// <summary>Gets the most recently created timer, or <see langword="null"/> if none has been created yet.</summary>
    public FakeTimer? LastTimer { get; private set; }

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(callback, state);
        LastTimer = timer;
        timer.Change(dueTime, period);
        return timer;
    }
}

/// <summary>
///     A fake <see cref="ITimer"/> whose callback a test invokes directly via <see cref="Fire"/>,
///     and whose <see cref="Change"/> calls are counted so a test can assert an idle timer was
///     re-armed the expected number of times.
/// </summary>
internal sealed class FakeTimer : ITimer
{
    private readonly TimerCallback _callback;
    private readonly object? _state;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeTimer"/> class.
    /// </summary>
    public FakeTimer(TimerCallback callback, object? state)
    {
        _callback = callback;
        _state = state;
    }

    /// <summary>Gets the number of times <see cref="Change"/> was called (including the initial arm).</summary>
    public int ChangeCallCount { get; private set; }

    /// <summary>Gets the due time given to the most recent <see cref="Change"/> call.</summary>
    public TimeSpan LastDueTime { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="Dispose"/> was called.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    ///     Records the re-arm request. Never actually schedules anything: a test fires the
    ///     callback explicitly via <see cref="Fire"/>.
    /// </summary>
    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        ChangeCallCount++;
        LastDueTime = dueTime;
        return true;
    }

    /// <summary>
    ///     Synchronously invokes the timer callback, simulating the idle window elapsing with no
    ///     reset.
    /// </summary>
    public void Fire() => _callback(_state);

    /// <inheritdoc/>
    public void Dispose() => IsDisposed = true;

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
