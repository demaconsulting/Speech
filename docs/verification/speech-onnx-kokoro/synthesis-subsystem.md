## SynthesisSubsystem Verification

### Verification Approach

The SpeechOnnxKokoro SynthesisSubsystem contains a single unit, `OnnxKokoroSynthesisEngine`, the
real, ONNX-Runtime-backed implementation of the Speech library's public `ISynthesisBackend` seam
over Kokoro v1.0's bare ONNX graph.

**No automated unit test project exists for this package** (there is no
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests` directory). This is a known, pre-existing gap, not
a deliberate design decision, and correctness today rests entirely on manual inspection and code
review, supplemented by a one-time manual Python validation spike (recorded directly in this
class's own XML documentation) that proved the real `onnx-community/Kokoro-82M-v1.0-ONNX` graph's
input/output tensor contract end-to-end against a real loaded session before this class was
written. All synthesis policy above the seam - Layer 2 rendering, chunking, the session state
machine, cancellation, and fault containment - remains fully verified in the Speech library
against a fake backend (see _Speech SynthesisSubsystem Verification_), and this model's engine
configuration is verified in _SpeechOnnxKokoro ModelManagementSubsystem Verification_.

A future pass may add a real `OnnxKokoroSynthesisEngineTests` class to a new
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests` project, mirroring the sibling SpeechSherpa
package's `SherpaOnnxSynthesisEngineTests` conventions (tests self-skipping, rather than failing,
when the real model files are not installed in the running environment). Adding that project is
explicitly out of scope for this pass.

### Test Environment

N/A - no automated test project exists for this subsystem.

### Unit-Level Test Scenarios

See the unit's own verification document: `onnx-kokoro-synthesis-engine.md`.

### Acceptance Criteria

This subsystem is accepted on the strength of: successful compilation with zero warnings, static
analysis via `Microsoft.CodeAnalysis.NetAnalyzers` and `SonarAnalyzer.CSharp`, the manual/
code-review verification described above, and the one-time manual Python validation spike proving
the real ONNX graph's tensor contract - not an automated test run.
