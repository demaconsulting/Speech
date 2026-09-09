### ISpeechRecognizer

#### Verification Approach

`ISpeechRecognizer` is verified indirectly through both shipped implementations:
`UnavailableSpeechRecognizer` proves the honest-unavailable path, and
`SherpaOnnxSpeechRecognizer` proves availability reporting, lifecycle behavior, and delivery of
provisional and final results. The result and event value types are verified through the results
observed at the contract's event, since they carry no behavior of their own.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner; no model, microphone, or native
  speech-inference runtime is required.

#### Acceptance Criteria

The contract is considered verified when the unavailable implementation reports `false`
availability, throws on operational misuse, and treats subscription and disposal as safe no-ops,
and when the real implementation starts and stops capture, delivers ordered provisional and final
results carrying the full recognized text, releases its engine on disposal, and supports many
independent Start/Stop cycles on the same instance without needing to be reconstructed.

#### Test Scenarios

See the RecognitionSubsystem-level scenarios "Pipeline: Result Delivery and Ordering", "Pipeline:
Lifecycle and Draining", and "Unavailable Fallback: Honest Degradation". The "construct once,
reuse across many turns" contract is verified directly by
`SherpaOnnxSpeechRecognizer_MultipleStartStopCycles_ReusesSameInstanceWithoutReconstruction`.
