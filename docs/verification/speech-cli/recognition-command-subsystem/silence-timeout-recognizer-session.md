### SilenceTimeoutRecognizerSession Verification

#### Verification Approach

`SilenceTimeoutRecognizerSession` is verified entirely in isolation in
`SilenceTimeoutRecognizerSessionTests`, against a hand-written `FakeRecognitionSession` and a
hand-written `FakeTimeProvider`/`FakeTimer` pair whose timer a test fires directly. Every idle,
reset and timeout scenario therefore runs deterministically with no real wall-clock delay. Tests
read the due time of the most recently armed timer to prove which window is in effect.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test builds its own session, time provider and wrapper; no shared state
- **Test doubles**: `FakeRecognitionSession` (raises scripted results, counts `StopAsync` calls and
  completes its result stream when stopped) and `FakeTimeProvider`/`FakeTimer` (records the due
  time each timer is armed with and fires on demand)

#### Acceptance Criteria

The unit passes when it rejects invalid construction arguments, arms the start window before the
first result and the idle window thereafter, stops the wrapped session and raises `TimedOut`
exactly once when the active window elapses, and propagates cancellation.

#### Test Scenarios

##### SilenceTimeoutRecognizerSession_Construct_NullSession_ThrowsArgumentNullException

**Scenario**: The wrapper is built over a null session.

**Expected**: `ArgumentNullException`.

##### SilenceTimeoutRecognizerSession_Construct_NonPositiveIdleTimeout_ThrowsArgumentOutOfRangeException

**Scenario**: The wrapper is built with an idle timeout of zero.

**Expected**: `ArgumentOutOfRangeException`.

##### SilenceTimeoutRecognizerSession_Construct_NonPositiveStartTimeout_ThrowsArgumentOutOfRangeException

**Scenario**: The wrapper is built with a start timeout of zero.

**Expected**: `ArgumentOutOfRangeException`.

##### SilenceTimeoutRecognizerSession_Construct_NullTimeProvider_DoesNotThrow

**Scenario**: The wrapper is built with no time provider.

**Expected**: No exception; the system clock is used.

##### SilenceTimeoutRecognizerSession_GetResultsAsync_StartTimeoutOmitted_ArmsTimerWithIdleTimeout

**Scenario**: A 7-second idle timeout and no start timeout; enumeration begins.

**Expected**: The first timer is armed for 7 seconds. Stopping the fake session then completes the
enumeration.

##### SilenceTimeoutRecognizerSession_GetResultsAsync_StartTimeoutGiven_ArmsTimerWithStartTimeout

**Scenario**: A 5-second idle timeout and a 2-second start timeout; enumeration begins.

**Expected**: The first timer is armed for 2 seconds.

##### SilenceTimeoutRecognizerSession_GetResultsAsync_FirstResultReceived_ReArmsWithIdleTimeoutNotStartTimeout

**Scenario**: With the 5-second and 2-second windows, one partial result arrives and the next
move is requested.

**Expected**: The result is yielded and the next timer is armed for 5 seconds, not 2.

##### SilenceTimeoutRecognizerSession_GetResultsAsync_SecondResultReceived_StaysOnIdleTimeout

**Scenario**: Two results arrive in turn, then a third move is requested.

**Expected**: Every timer armed after the first result uses the 5-second idle window.

##### SilenceTimeoutRecognizerSession_GetResultsAsync_TimeoutBeforeAnyResult_StopsSessionAndRaisesTimedOutOnce

**Scenario**: The start-window timer fires before any result.

**Expected**: The enumeration completes, the session's `StopAsync` was called once and `TimedOut`
was raised once.

##### SilenceTimeoutRecognizerSession_GetResultsAsync_TimeoutAfterResult_StopsSessionAndRaisesTimedOutOnce

**Scenario**: One result is yielded, then the idle-window timer fires.

**Expected**: The enumeration completes, `StopAsync` was called once and `TimedOut` was raised
once. No test proves that a trailing result drained by the stop is still yielded; the behavior is
described in the design document only.

##### SilenceTimeoutRecognizerSession_GetResultsAsync_CancellationRequested_PropagatesOperationCanceledException

**Scenario**: The token passed to `GetResultsAsync` is canceled while a move is pending.

**Expected**: The pending move throws `OperationCanceledException`; the cancellation is not
swallowed or turned into a timeout.
