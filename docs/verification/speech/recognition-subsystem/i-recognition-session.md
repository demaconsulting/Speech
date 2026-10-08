### IRecognitionSession

#### Verification Approach

`IRecognitionSession` is verified indirectly through both shipped implementations:
`UnavailableRecognitionSession` proves the honest-unavailable path, and
`RecognitionSession` proves state-machine transitions, lifecycle behavior, and delivery
of provisional and final results. The result and event value types are verified through the
results observed at `GetResultsAsync`, since they carry no behavior of their own.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner; no model, microphone, or native
  speech-inference runtime is required.

#### Acceptance Criteria

The contract is considered verified when the unavailable implementation reports `false`
availability, reports `State` as `Created`, throws on operational misuse, and treats subscription
and disposal as safe no-ops, and when the real implementation only makes forward-only, documented
`RecognitionSessionState` transitions, raises `StateChanged` for each one, starts and stops
capture, delivers ordered provisional and final results carrying the full recognized text -
including finalizing and delivering trailing audio accepted but not yet decoded before
`StopAsync()` returns rather than losing it - releases its engine lease on disposal, and is
single-use and single-consumer by contract.

#### Test Scenarios

See the RecognitionSubsystem-level scenarios "Session Lifecycle: State Machine Transitions",
"Session Lifecycle: Stop, Dispose, and Draining", "Session Pipeline: Result Delivery and
Backpressure", and "Unavailable Fallback: Honest Degradation".
