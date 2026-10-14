### UnavailableRecognitionSession

#### Verification Approach

`UnavailableRecognitionSession` is verified directly against its shared instance; it has no
dependencies to mock. `SpeechRecognizerUnavailableException` is verified through its three
constructors. The safe-no-op test deliberately disposes the shared instance twice and then
re-reads it, proving that a host wrapping its session in a disposal scope cannot invalidate the
fallback for anyone else.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The unit is considered verified when the shared instance reports `false` availability and
`Created` state, throws `SpeechRecognizerUnavailableException` from `StartAsync` and from the
`GetResultsAsync` call itself (before any enumeration), treats repeated `StopAsync` calls and
repeated disposal as safe no-ops, and
the exception type exposes the message and inner exception supplied to each of its constructors.

#### Test Scenarios

##### UnavailableRecognitionSession: Honest Unavailable Behavior

**Tests**: `UnavailableRecognitionSession_IsAvailable_Read_ReturnsFalse`,
`UnavailableRecognitionSession_StartAsync_Always_ThrowsSpeechRecognizerUnavailableException`,
`UnavailableRecognitionSession_GetResultsAsync_Always_ThrowsSpeechRecognizerUnavailableException`

Verifies that the shared instance reports itself unavailable and that both starting and
requesting results fail with `SpeechRecognizerUnavailableException` rather than hanging or
silently yielding nothing.

##### UnavailableRecognitionSession: Safe No-Op Stop and Repeated Disposal

**Tests**: `UnavailableRecognitionSession_StopAsync_Always_IsSafeNoOp`,
`UnavailableRecognitionSession_DisposeAsync_CalledTwice_DoesNotThrow`

Verifies that repeated stopping is a no-op and that disposing the shared instance twice, then re-reading it,
leaves the fallback intact for every other holder. The RecognitionSubsystem-level scenario
"Unavailable Fallback: Honest Degradation" covers the same tests alongside the engine fallback.
