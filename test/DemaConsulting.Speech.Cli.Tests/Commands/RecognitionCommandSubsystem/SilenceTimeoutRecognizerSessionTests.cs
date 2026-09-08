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

using DemaConsulting.Speech.Cli.Commands.RecognitionCommandSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.RecognitionCommandSubsystem;

/// <summary>
///     Unit tests for <see cref="SilenceTimeoutRecognizerSession"/>, using
///     <see cref="FakeSpeechRecognizer"/> and <see cref="FakeTimeProvider"/> so every scenario
///     runs deterministically with no real wall-clock delay.
/// </summary>
public sealed class SilenceTimeoutRecognizerSessionTests
{
    /// <summary>Test that construction arms the idle timer once, with the given timeout.</summary>
    [Fact]
    public void SilenceTimeoutRecognizerSession_Construct_ArmsTimerWithGivenTimeout()
    {
        var recognizer = new FakeSpeechRecognizer();
        var timeProvider = new FakeTimeProvider();

        using var session = new SilenceTimeoutRecognizerSession(recognizer, TimeSpan.FromSeconds(5), timeProvider);

        Assert.NotNull(timeProvider.LastTimer);
        Assert.Equal(1, timeProvider.LastTimer.ChangeCallCount);
        Assert.Equal(TimeSpan.FromSeconds(5), timeProvider.LastTimer.LastDueTime);
    }

    /// <summary>Test that a partial (non-final) result re-arms the idle timer.</summary>
    [Fact]
    public void SilenceTimeoutRecognizerSession_PartialResultReceived_ResetsIdleTimer()
    {
        var recognizer = new FakeSpeechRecognizer();
        var timeProvider = new FakeTimeProvider();
        using var session = new SilenceTimeoutRecognizerSession(recognizer, TimeSpan.FromSeconds(5), timeProvider);

        recognizer.RaiseResult("hel", isFinal: false);

        Assert.Equal(2, timeProvider.LastTimer!.ChangeCallCount);
    }

    /// <summary>Test that a final result re-arms the idle timer.</summary>
    [Fact]
    public void SilenceTimeoutRecognizerSession_FinalResultReceived_ResetsIdleTimer()
    {
        var recognizer = new FakeSpeechRecognizer();
        var timeProvider = new FakeTimeProvider();
        using var session = new SilenceTimeoutRecognizerSession(recognizer, TimeSpan.FromSeconds(5), timeProvider);

        recognizer.RaiseResult("hello", isFinal: true);

        Assert.Equal(2, timeProvider.LastTimer!.ChangeCallCount);
    }

    /// <summary>Test that the idle timer firing with no reset stops the recognizer and raises TimedOut.</summary>
    [Fact]
    public void SilenceTimeoutRecognizerSession_IdleTimerFires_StopsRecognizerAndRaisesTimedOut()
    {
        var recognizer = new FakeSpeechRecognizer();
        var timeProvider = new FakeTimeProvider();
        using var session = new SilenceTimeoutRecognizerSession(recognizer, TimeSpan.FromSeconds(5), timeProvider);
        var timedOutRaised = false;
        session.TimedOut += (_, _) => timedOutRaised = true;

        timeProvider.LastTimer!.Fire();

        Assert.Equal(1, recognizer.StopCallCount);
        Assert.True(timedOutRaised);
    }

    /// <summary>Test that resetting before the timer fires prevents a stale timeout from acting (defensive: a later Fire still only calls Stop once here since the fake never auto-cancels a prior "due" state, so this proves the reset call count increased and Stop still reflects a genuine Fire call).</summary>
    [Fact]
    public void SilenceTimeoutRecognizerSession_ResetThenFire_StopsOnlyOnActualFire()
    {
        var recognizer = new FakeSpeechRecognizer();
        var timeProvider = new FakeTimeProvider();
        using var session = new SilenceTimeoutRecognizerSession(recognizer, TimeSpan.FromSeconds(5), timeProvider);

        recognizer.RaiseResult("still talking", isFinal: false);
        Assert.Equal(0, recognizer.StopCallCount);

        timeProvider.LastTimer!.Fire();
        Assert.Equal(1, recognizer.StopCallCount);
    }

    /// <summary>Test that Dispose unsubscribes and disposes the timer, and is idempotent.</summary>
    [Fact]
    public void SilenceTimeoutRecognizerSession_Dispose_UnsubscribesAndDisposesTimer()
    {
        var recognizer = new FakeSpeechRecognizer();
        var timeProvider = new FakeTimeProvider();
        var session = new SilenceTimeoutRecognizerSession(recognizer, TimeSpan.FromSeconds(5), timeProvider);

        session.Dispose();
        session.Dispose();

        Assert.True(timeProvider.LastTimer!.IsDisposed);

        // A result raised after disposal must not re-arm the (now disposed) timer.
        var changeCallCountAfterDispose = timeProvider.LastTimer.ChangeCallCount;
        recognizer.RaiseResult("late", isFinal: true);
        Assert.Equal(changeCallCountAfterDispose, timeProvider.LastTimer.ChangeCallCount);
    }

    /// <summary>
    ///     Test that a timer fire racing a concurrent <see cref="SilenceTimeoutRecognizerSession.Dispose"/>
    ///     call (simulated deterministically: dispose first, then invoke the callback the fake
    ///     timer would otherwise have fired) does not call <c>Stop()</c> or raise <c>TimedOut</c>
    ///     on the already-disposed session.
    /// </summary>
    [Fact]
    public void SilenceTimeoutRecognizerSession_FireAfterDispose_DoesNotCallStopOrRaiseTimedOut()
    {
        var recognizer = new FakeSpeechRecognizer();
        var timeProvider = new FakeTimeProvider();
        var session = new SilenceTimeoutRecognizerSession(recognizer, TimeSpan.FromSeconds(5), timeProvider);
        var timedOutRaised = false;
        session.TimedOut += (_, _) => timedOutRaised = true;

        session.Dispose();
        timeProvider.LastTimer!.Fire();

        Assert.Equal(0, recognizer.StopCallCount);
        Assert.False(timedOutRaised);
    }

    /// <summary>Test that a null recognizer is rejected.</summary>
    [Fact]
    public void SilenceTimeoutRecognizerSession_Construct_NullRecognizer_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SilenceTimeoutRecognizerSession(null!, TimeSpan.FromSeconds(5), new FakeTimeProvider()));
    }

    /// <summary>Test that a non-positive idle timeout is rejected.</summary>
    [Fact]
    public void SilenceTimeoutRecognizerSession_Construct_NonPositiveTimeout_ThrowsArgumentOutOfRangeException()
    {
        var recognizer = new FakeSpeechRecognizer();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SilenceTimeoutRecognizerSession(recognizer, TimeSpan.Zero, new FakeTimeProvider()));
    }

    /// <summary>Test that a null timeProvider defaults to TimeProvider.System without throwing.</summary>
    [Fact]
    public void SilenceTimeoutRecognizerSession_Construct_NullTimeProvider_UsesSystemTimeProvider()
    {
        var recognizer = new FakeSpeechRecognizer();

        using var session = new SilenceTimeoutRecognizerSession(recognizer, TimeSpan.FromMinutes(10));

        // Constructing does not throw and does not itself call Stop/dispose prematurely.
        Assert.Equal(0, recognizer.StopCallCount);
    }
}
