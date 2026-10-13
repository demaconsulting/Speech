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
///     <see cref="FakeRecognitionSession"/> and <see cref="FakeTimeProvider"/> so every scenario
///     runs deterministically with no real wall-clock delay.
/// </summary>
/// <remarks>
///     <see cref="SilenceTimeoutRecognizerSession"/> is a stateless <c>async</c>-iterator
///     decorator with no background thread and no <see cref="IDisposable"/>/<see cref="IAsyncDisposable"/>
///     surface, so (unlike the previous synchronous, thread-based implementation) there are no
///     thread-race or disposal-ordering scenarios to cover here. Each test drives the decorator by
///     obtaining its <see cref="IAsyncEnumerator{T}"/> directly and calling
///     <c>MoveNextAsync()</c> without immediately awaiting it: per the type's own remarks, the
///     compiler-generated iterator runs synchronously up to its first genuine suspension point
///     (the race between the inner session's next result and the idle-timeout delay), so
///     <see cref="FakeTimeProvider.LastTimer"/> is already armed by the time the unawaited
///     <c>ValueTask</c> is returned.
/// </remarks>
public sealed class SilenceTimeoutRecognizerSessionTests
{
    /// <summary>Test that a null session is rejected.</summary>
    [Fact]
    public void SilenceTimeoutRecognizerSession_Construct_NullSession_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => new SilenceTimeoutRecognizerSession(null!, TimeSpan.FromSeconds(5), new FakeTimeProvider()));
    }

    /// <summary>Test that a non-positive idle timeout is rejected.</summary>
    [Fact]
    public void SilenceTimeoutRecognizerSession_Construct_NonPositiveIdleTimeout_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var session = new FakeRecognitionSession();

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SilenceTimeoutRecognizerSession(session, TimeSpan.Zero, new FakeTimeProvider()));
    }

    /// <summary>Test that a non-positive start timeout is rejected.</summary>
    [Fact]
    public void SilenceTimeoutRecognizerSession_Construct_NonPositiveStartTimeout_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var session = new FakeRecognitionSession();

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SilenceTimeoutRecognizerSession(
                session,
                TimeSpan.FromSeconds(5),
                new FakeTimeProvider(),
                startTimeout: TimeSpan.Zero));
    }

    /// <summary>Test that a null time provider is accepted, defaulting to the system clock.</summary>
    [Fact]
    public void SilenceTimeoutRecognizerSession_Construct_NullTimeProvider_DoesNotThrow()
    {
        // Arrange
        var session = new FakeRecognitionSession();

        // Act
        var exception = Record.Exception(() => new SilenceTimeoutRecognizerSession(session, TimeSpan.FromMinutes(10)));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Test that, with no time source supplied, the idle window elapses in real time: a short
    ///     window ends the session after that much wall-clock time with no result.
    /// </summary>
    [Fact]
    public async Task SilenceTimeoutRecognizerSession_GetResultsAsync_NoTimeProvider_EndsSessionAfterRealIdleWindow()
    {
        // Arrange
        var session = new FakeRecognitionSession();
        var wrapper = new SilenceTimeoutRecognizerSession(session, TimeSpan.FromMilliseconds(100));
        var timedOutCount = 0;
        wrapper.TimedOut += (_, _) => timedOutCount++;
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var results = new List<DemaConsulting.Speech.RecognitionSubsystem.SpeechRecognitionEvent>();
        await foreach (var e in wrapper.GetResultsAsync(cancellationToken))
        {
            results.Add(e);
        }

        // Assert
        Assert.Empty(results);
        Assert.Equal(1, session.StopCallCount);
        Assert.Equal(1, timedOutCount);
    }

    /// <summary>Test that enumeration, with no start timeout given, arms the idle timer with the idle timeout.</summary>
    [Fact]
    public async Task SilenceTimeoutRecognizerSession_GetResultsAsync_StartTimeoutOmitted_ArmsTimerWithIdleTimeout()
    {
        // Arrange
        var session = new FakeRecognitionSession();
        var timeProvider = new FakeTimeProvider();
        var wrapper = new SilenceTimeoutRecognizerSession(session, TimeSpan.FromSeconds(7), timeProvider);

        using var cts = new CancellationTokenSource();
        await using var enumerator = wrapper.GetResultsAsync(cts.Token).GetAsyncEnumerator(cts.Token);

        // Act
        var moveNextTask = enumerator.MoveNextAsync();

        // Assert
        Assert.NotNull(timeProvider.LastTimer);
        Assert.Equal(TimeSpan.FromSeconds(7), timeProvider.LastTimer.LastDueTime);

        // Drain cleanly so the pending move-next settles before the test ends.
        await session.StopAsync(TestContext.Current.CancellationToken);
        Assert.False(await moveNextTask);
    }

    /// <summary>Test that enumeration, with a start timeout given, arms the idle timer with the start timeout.</summary>
    [Fact]
    public async Task SilenceTimeoutRecognizerSession_GetResultsAsync_StartTimeoutGiven_ArmsTimerWithStartTimeout()
    {
        // Arrange
        var session = new FakeRecognitionSession();
        var timeProvider = new FakeTimeProvider();
        var wrapper = new SilenceTimeoutRecognizerSession(
            session,
            TimeSpan.FromSeconds(5),
            timeProvider,
            startTimeout: TimeSpan.FromSeconds(2));

        using var cts = new CancellationTokenSource();
        await using var enumerator = wrapper.GetResultsAsync(cts.Token).GetAsyncEnumerator(cts.Token);

        // Act
        var moveNextTask = enumerator.MoveNextAsync();

        // Assert
        Assert.NotNull(timeProvider.LastTimer);
        Assert.Equal(TimeSpan.FromSeconds(2), timeProvider.LastTimer.LastDueTime);

        await session.StopAsync(TestContext.Current.CancellationToken);
        Assert.False(await moveNextTask);
    }

    /// <summary>Test that the first yielded result re-arms the timer with the idle timeout, not the start timeout.</summary>
    [Fact]
    public async Task SilenceTimeoutRecognizerSession_GetResultsAsync_FirstResultReceived_ReArmsWithIdleTimeoutNotStartTimeout()
    {
        // Arrange
        var session = new FakeRecognitionSession();
        var timeProvider = new FakeTimeProvider();
        var wrapper = new SilenceTimeoutRecognizerSession(
            session,
            TimeSpan.FromSeconds(5),
            timeProvider,
            startTimeout: TimeSpan.FromSeconds(2));

        using var cts = new CancellationTokenSource();
        await using var enumerator = wrapper.GetResultsAsync(cts.Token).GetAsyncEnumerator(cts.Token);

        // Act
        var firstMoveNextTask = enumerator.MoveNextAsync();
        Assert.Equal(TimeSpan.FromSeconds(2), timeProvider.LastTimer!.LastDueTime);

        session.RaiseResult("hel", isFinal: false);
        Assert.True(await firstMoveNextTask);
        Assert.Equal("hel", enumerator.Current.Result.Text);

        var secondMoveNextTask = enumerator.MoveNextAsync();

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(5), timeProvider.LastTimer.LastDueTime);

        await session.StopAsync(TestContext.Current.CancellationToken);
        Assert.False(await secondMoveNextTask);
    }

    /// <summary>Test that a second yielded result keeps the timer re-armed with the idle timeout.</summary>
    [Fact]
    public async Task SilenceTimeoutRecognizerSession_GetResultsAsync_SecondResultReceived_StaysOnIdleTimeout()
    {
        // Arrange
        var session = new FakeRecognitionSession();
        var timeProvider = new FakeTimeProvider();
        var wrapper = new SilenceTimeoutRecognizerSession(
            session,
            TimeSpan.FromSeconds(5),
            timeProvider,
            startTimeout: TimeSpan.FromSeconds(2));

        using var cts = new CancellationTokenSource();
        await using var enumerator = wrapper.GetResultsAsync(cts.Token).GetAsyncEnumerator(cts.Token);

        // Act
        var firstMoveNextTask = enumerator.MoveNextAsync();

        session.RaiseResult("hel", isFinal: false);
        Assert.True(await firstMoveNextTask);

        var secondMoveNextTask = enumerator.MoveNextAsync();
        session.RaiseResult("hello", isFinal: true);
        Assert.True(await secondMoveNextTask);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(5), timeProvider.LastTimer!.LastDueTime);

        var thirdMoveNextTask = enumerator.MoveNextAsync();
        Assert.Equal(TimeSpan.FromSeconds(5), timeProvider.LastTimer.LastDueTime);

        await session.StopAsync(TestContext.Current.CancellationToken);
        Assert.False(await thirdMoveNextTask);
    }

    /// <summary>Test that a timeout before any result stops the session and raises <c>TimedOut</c> exactly once.</summary>
    [Fact]
    public async Task SilenceTimeoutRecognizerSession_GetResultsAsync_TimeoutBeforeAnyResult_StopsSessionAndRaisesTimedOutOnce()
    {
        // Arrange
        var session = new FakeRecognitionSession();
        var timeProvider = new FakeTimeProvider();
        var wrapper = new SilenceTimeoutRecognizerSession(
            session,
            TimeSpan.FromSeconds(5),
            timeProvider,
            startTimeout: TimeSpan.FromSeconds(2));
        var timedOutCount = 0;
        wrapper.TimedOut += (_, _) => timedOutCount++;

        using var cts = new CancellationTokenSource();
        await using var enumerator = wrapper.GetResultsAsync(cts.Token).GetAsyncEnumerator(cts.Token);

        // Act
        var moveNextTask = enumerator.MoveNextAsync();

        timeProvider.LastTimer!.Fire();

        // Assert
        Assert.False(await moveNextTask);
        Assert.Equal(1, session.StopCallCount);
        Assert.Equal(1, timedOutCount);
    }

    /// <summary>Test that a timeout after a result stops the session and raises <c>TimedOut</c> exactly once.</summary>
    [Fact]
    public async Task SilenceTimeoutRecognizerSession_GetResultsAsync_TimeoutAfterResult_StopsSessionAndRaisesTimedOutOnce()
    {
        // Arrange
        var session = new FakeRecognitionSession();
        var timeProvider = new FakeTimeProvider();
        var wrapper = new SilenceTimeoutRecognizerSession(session, TimeSpan.FromSeconds(5), timeProvider);
        var timedOutCount = 0;
        wrapper.TimedOut += (_, _) => timedOutCount++;

        using var cts = new CancellationTokenSource();
        await using var enumerator = wrapper.GetResultsAsync(cts.Token).GetAsyncEnumerator(cts.Token);

        // Act
        var firstMoveNextTask = enumerator.MoveNextAsync();

        session.RaiseResult("hel", isFinal: false);
        Assert.True(await firstMoveNextTask);

        var secondMoveNextTask = enumerator.MoveNextAsync();
        timeProvider.LastTimer!.Fire();

        // Assert
        Assert.False(await secondMoveNextTask);
        Assert.Equal(1, session.StopCallCount);
        Assert.Equal(1, timedOutCount);
    }

    /// <summary>Test that cancelling the token passed to <c>GetResultsAsync</c> propagates a cancellation from the enumeration.</summary>
    [Fact]
    public async Task SilenceTimeoutRecognizerSession_GetResultsAsync_CancellationRequested_PropagatesOperationCanceledException()
    {
        // Arrange
        var session = new FakeRecognitionSession();
        var timeProvider = new FakeTimeProvider();
        var wrapper = new SilenceTimeoutRecognizerSession(session, TimeSpan.FromSeconds(5), timeProvider);

        using var cts = new CancellationTokenSource();
        await using var enumerator = wrapper.GetResultsAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        var moveNextTask = enumerator.MoveNextAsync();

        // Act
        await cts.CancelAsync();

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await moveNextTask);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    /// <summary>
    ///     Regression test: the idle timer of every iteration is canceled once a result wins the
    ///     race, so no pending timers accumulate across results.
    /// </summary>
    [Fact]
    public async Task SilenceTimeoutRecognizerSession_GetResultsAsync_ManyResults_LeavesNoPendingTimers()
    {
        // Arrange
        const int resultCount = 5;
        var session = new FakeRecognitionSession();
        var timeProvider = new FakeTimeProvider();
        var wrapper = new SilenceTimeoutRecognizerSession(session, TimeSpan.FromSeconds(5), timeProvider);
        using var cts = new CancellationTokenSource();
        await using var enumerator = wrapper.GetResultsAsync(cts.Token).GetAsyncEnumerator(cts.Token);

        // Act / Assert
        for (var i = 0; i < resultCount; i++)
        {
            var moveNextTask = enumerator.MoveNextAsync();
            Assert.Equal(1, timeProvider.ActiveTimerCount);
            session.RaiseResult($"result {i}", isFinal: false);
            Assert.True(await moveNextTask);
            Assert.Equal(0, timeProvider.ActiveTimerCount);
        }

        var lastMoveNextTask = enumerator.MoveNextAsync();
        Assert.Equal(1, timeProvider.ActiveTimerCount);
        await session.StopAsync(TestContext.Current.CancellationToken);
        Assert.False(await lastMoveNextTask);
        Assert.Equal(resultCount + 1, timeProvider.Timers.Count);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    /// <summary>Test that the timer that lost the race against a yielded result is disposed (canceled).</summary>
    [Fact]
    public async Task SilenceTimeoutRecognizerSession_GetResultsAsync_ResultWinsRace_DisposesLosingTimer()
    {
        // Arrange
        var session = new FakeRecognitionSession();
        var timeProvider = new FakeTimeProvider();
        var wrapper = new SilenceTimeoutRecognizerSession(session, TimeSpan.FromSeconds(5), timeProvider);
        using var cts = new CancellationTokenSource();
        await using var enumerator = wrapper.GetResultsAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        var moveNextTask = enumerator.MoveNextAsync();
        var firstTimer = timeProvider.LastTimer!;

        // Act
        session.RaiseResult("hello", isFinal: true);
        await moveNextTask;

        // Assert
        Assert.True(firstTimer.IsDisposed);
    }

    /// <summary>
    ///     Test that a trailing result produced by the timeout-triggered <c>StopAsync</c> drain is
    ///     still yielded before the enumeration ends.
    /// </summary>
    [Fact]
    public async Task SilenceTimeoutRecognizerSession_GetResultsAsync_TimeoutDrain_YieldsTrailingResultBeforeEnding()
    {
        // Arrange
        var session = new FakeRecognitionSession();
        session.OnStop = s => s.RaiseResult("trailing", isFinal: true);
        var timeProvider = new FakeTimeProvider();
        var wrapper = new SilenceTimeoutRecognizerSession(session, TimeSpan.FromSeconds(5), timeProvider);
        using var cts = new CancellationTokenSource();
        await using var enumerator = wrapper.GetResultsAsync(cts.Token).GetAsyncEnumerator(cts.Token);

        // Act
        var firstMoveNextTask = enumerator.MoveNextAsync();
        timeProvider.LastTimer!.Fire();
        var hasTrailing = await firstMoveNextTask;
        var trailingText = enumerator.Current.Result.Text;
        var hasMore = await enumerator.MoveNextAsync();

        // Assert
        Assert.True(hasTrailing);
        Assert.Equal("trailing", trailingText);
        Assert.False(hasMore);
        Assert.Equal(1, session.StopCallCount);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }
}
