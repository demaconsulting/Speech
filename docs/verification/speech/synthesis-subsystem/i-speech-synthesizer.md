### ISpeechSynthesizer

#### Verification Approach

`ISpeechSynthesizer` is verified indirectly through both shipped implementations:
`UnavailableSpeechSynthesizer` proves the honest-unavailable path, and
`SherpaOnnxSpeechSynthesizer` proves availability reporting, chunked/pipelined synthesis and
playback, and cancellation. The `SynthesizedSpeech` value type is verified through the segments
observed from `SynthesizeStreamAsync`, since it carries no behavior of its own.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner; no model, speakers, or native
  speech-inference runtime is required.

#### Acceptance Criteria

The contract is considered verified when the unavailable implementation reports `false`
availability and throws on every operational member, and when the real implementation reports
itself available, yields ordered synthesized segments with correct pre/post silence, plays them
in order, cancels an in-flight session deterministically on `Stop()`, and supports many
independent synthesize/play sessions on the same instance without needing to be reconstructed.

#### Test Scenarios

See the SynthesisSubsystem-level scenarios "Pipeline: Chunked Synthesis and Ordered Playback",
"Pipeline: Cancellation and Lifecycle", and "Unavailable Fallback: Honest Degradation". The
"construct once, reuse across many turns" contract is verified directly by
`PlayStreamAsync_CalledTwiceOnSameInstance_ReusesSameInstanceWithoutReconstruction`.
