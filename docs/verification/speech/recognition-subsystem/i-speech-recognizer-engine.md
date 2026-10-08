### ISpeechRecognizerEngine

#### Verification Approach

`ISpeechRecognizerEngine` is verified indirectly through both shipped implementations:
`UnavailableSpeechRecognizerEngine` proves the honest-unavailable path, and
`SpeechRecognizerEngine` proves availability reporting and the engine's single-lease
exclusivity behavior.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner; no model, microphone, or native
  speech-inference runtime is required.

#### Acceptance Criteria

The contract is considered verified when the unavailable implementation reports `false`
availability and always returns the shared unavailable session, and when the real implementation
leases its backend to exactly one live session at a time, throwing `RecognitionEngineBusyException`
for a concurrent `CreateSessionAsync` attempt and permitting a new session once the prior one is
fully disposed.

#### Test Scenarios

See the RecognitionSubsystem-level scenarios "Engine Exclusivity and Lease Behavior" and
"Unavailable Fallback: Honest Degradation".
