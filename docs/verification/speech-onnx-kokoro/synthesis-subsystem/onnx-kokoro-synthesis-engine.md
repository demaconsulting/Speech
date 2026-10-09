### OnnxKokoroSynthesisEngine Verification

#### Verification Approach

**No automated unit test project exists for this package** (see _SpeechOnnxKokoro System
Verification_); none of this unit's requirements link to a test, because none exists. This class
is verified by manual inspection and code review:

- Its constructor's argument validation (null checks, empty `voiceStylesBySpeakerId` check) was
  reviewed by inspection
- Its `SampleRate` constant (24000 Hz) was reviewed against the real
  `onnx-community/Kokoro-82M-v1.0-ONNX` README
- Its `Generate` method's phonemization -> token-id conversion -> padding -> style-vector
  selection -> ONNX Runtime forward-pass sequence was reviewed by inspection, and was additionally
  proven end-to-end against a real loaded ONNX Runtime session by a one-time manual Python
  validation spike performed before this class was written (recorded directly in this class's own
  XML documentation), confirming the `input_ids`/`style`/`speed` input tensor contract and the
  single raw waveform output tensor
- Its empty-phoneme short-circuit was reviewed by inspection against `Generate`'s own early-return
  branch
- Its `SelectStyleVector` row-selection and unrecognized-speaker-id fallback were reviewed by
  inspection against the proven Python reference pipeline's own `voices[len(ids)]` lookup
- Its `Dispose` idempotency was reviewed by inspection of its `_disposed` guard

A future pass may add `OnnxKokoroSynthesisEngineTests` to a new
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests` project, mirroring the sibling SpeechSherpa
package's `SherpaOnnxSynthesisEngineTests` conventions (tests self-skipping, rather than failing,
when the real model files are not installed in the running environment). Adding that project is
explicitly out of scope for this pass.

#### Test Environment

N/A - no automated test project exists for this unit.

#### Acceptance Criteria

This unit is accepted on the strength of: successful compilation with zero warnings, static
analysis via `Microsoft.CodeAnalysis.NetAnalyzers` and `SonarAnalyzer.CSharp`, the manual/
code-review verification described above, and the one-time manual Python validation spike proving
the real ONNX graph's tensor contract end-to-end - not an automated test run.

#### Test Scenarios

N/A - no automated test scenarios exist for this unit.
