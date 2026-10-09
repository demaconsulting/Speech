## OnnxRuntimeSubsystem Verification

### Verification Approach

The SpeechOnnx OnnxRuntimeSubsystem contains a single unit, `OnnxExecutionProviderSelector`, and
has **no automated test coverage today** - there is no `test/DemaConsulting.Speech.Onnx.Tests`
project in this repository. This is a known, pre-existing gap, not a mocking/stubbing design
decision: nothing in this subsystem has been exercised by an automated test, mocked or otherwise.
Correctness of its current, fixed behavior - disposing each candidate `SessionOptions` immediately
after its own `InferenceSession` construction attempt, win or lose, without disposing the returned
`InferenceSession` on success - rests today solely on code review evidence (this fix was reviewed
directly against the source during this pass), not on executed test evidence.

This document still records the test scenarios a future unit test suite should implement, mirroring
how this repository verifies its other, similarly-shaped single-unit subsystems once they have a
test project (for example SpeechSherpa's RecognitionSubsystem and SynthesisSubsystem).

### Test Environment

No automated test environment exists yet for this subsystem. A future test project would run
under xUnit v3 via `dotnet test`, and would need only the base `Microsoft.ML.OnnxRuntime` CPU
provider (already referenced) plus a small valid `.onnx` fixture model - no network access, no
physical audio hardware, and no accelerated-provider native runtime strictly required (tests
asserting accelerated-provider fallback behavior would self-skip or assert the CPU fallback path
when no such runtime is present).

### Unit-Level Test Scenarios

See the unit's own verification document: _SpeechOnnx OnnxExecutionProviderSelector
Verification_ (`onnx-execution-provider-selector.md`).

### Acceptance Criteria

Not yet measurable by automated means. Once a test project exists, a passing subsystem-level test
run should require: a preferred provider whose native runtime is present is used to construct the
returned session; a preferred provider whose native runtime is absent is abandoned in favor of the
next candidate (or CPU) and its `SessionOptions` is confirmed disposed; the CPU fallback
candidate always succeeds for a well-formed model when no preferred provider is supplied or all
preferred providers fail; and `Create` throws `ArgumentException` for a null or empty model path
before attempting any provider.
