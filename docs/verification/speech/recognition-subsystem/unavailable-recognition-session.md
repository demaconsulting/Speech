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
`Faulted` state, throws `SpeechRecognizerUnavailableException` from `StartAsync` and the first
`MoveNextAsync` of `GetResultsAsync`, treats `StopAsync` and repeated disposal as safe no-ops, and
the exception type exposes the message and inner exception supplied to each of its constructors.

#### Test Scenarios

See the RecognitionSubsystem-level scenario "Unavailable Fallback: Honest Degradation".
