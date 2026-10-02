### UnavailableSpeechRecognizerEngine

#### Verification Approach

`UnavailableSpeechRecognizerEngine` is verified directly against its shared instance; it has no
dependencies to mock.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The unit is considered verified when the shared instance reports `false` availability, always
returns the shared `UnavailableRecognitionSession.Instance` from `CreateSessionAsync` regardless
of the supplied device, rejects a null device with `ArgumentNullException`, and treats repeated
disposal as a safe no-op.

#### Test Scenarios

See the RecognitionSubsystem-level scenario "Unavailable Fallback: Honest Degradation".
