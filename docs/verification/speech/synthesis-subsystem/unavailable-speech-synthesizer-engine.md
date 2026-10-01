### UnavailableSpeechSynthesizerEngine

#### Verification Approach

`UnavailableSpeechSynthesizerEngine` is verified directly against its shared instance; it has no
dependencies to mock. `CreateSessionAsync` is verified to succeed, returning the shared
unavailable session, since binding a device to an already unavailable engine is an ordinary (if
useless) composition, not an error. `SpeechSynthesizerUnavailableException` is verified through
its three constructors. The safe-dispose test deliberately disposes the shared instance twice,
proving that a host wrapping its engine in a disposal scope cannot invalidate the fallback for
anyone else.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The unit is considered verified when the shared instance reports `false` availability, always
succeeds at `CreateSessionAsync` (rejecting only a null device), throws
`SpeechSynthesizerUnavailableException` from `SpeakAsync`/`SynthesizeAsync`, treats repeated
disposal as a no-op, and the exception type exposes the message and inner exception supplied to
each of its constructors.

#### Test Scenarios

See the SynthesisSubsystem-level scenario "Unavailable Fallback: Honest Degradation".
