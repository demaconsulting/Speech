### ISpeechSynthesizerEngine

#### Verification Approach

`ISpeechSynthesizerEngine` is verified indirectly through both shipped implementations:
`UnavailableSpeechSynthesizerEngine` proves the honest-unavailable path, and
`SherpaOnnxSpeechSynthesizerEngine` proves availability reporting, session exclusivity leasing,
and the one-shot `SpeakAsync`/`SynthesizeAsync` convenience overloads. The `SynthesizedSpeech`
value type is verified through the segments observed from `SynthesizeAsync`, since it carries no
behavior of its own.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner; no model, speakers, or native
  speech-inference runtime is required.

#### Acceptance Criteria

The contract is considered verified when the unavailable implementation reports `false`
availability and throws on every operational member (while `CreateSessionAsync` itself still
succeeds, returning an unavailable session), and when the real implementation reports itself
available, enforces single-session exclusivity (`SynthesisEngineBusyException` on a concurrent
lease request), allows a new session once a prior one is disposed, and its one-shot convenience
overloads create and dispose a session per call.

#### Test Scenarios

See the SynthesisSubsystem-level scenarios "Engine: Session Exclusivity and Lease Lifecycle",
"Engine: One-Shot Convenience Overloads", and "Unavailable Fallback: Honest Degradation".
