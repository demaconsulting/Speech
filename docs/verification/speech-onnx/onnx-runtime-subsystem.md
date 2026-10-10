## OnnxRuntimeSubsystem Verification

### Verification Approach

The SpeechOnnx OnnxRuntimeSubsystem contains a single unit, `OnnxExecutionProviderSelector`,
verified by automated unit tests in `test/DemaConsulting.Speech.Onnx.Tests` against a tiny,
hand-built, valid `.onnx` fixture model. No accelerated execution provider (CUDA, DirectML, etc.)
can be genuinely exercised in this test project, since this package references only the base,
CPU-only `Microsoft.ML.OnnxRuntime` package; the tests use the always-available
`"CPUExecutionProvider"` string as a stand-in candidate, and a syntactically-invalid provider name
to reach the "construction itself fails" path, to reach the same construct/probe/dispose/fall-back
code paths a real accelerated candidate would exercise. See _SpeechOnnx
OnnxExecutionProviderSelector Verification_ for the full detail, including how the otherwise
unobservable "every candidate's `SessionOptions` is disposed" claim is proven via the internal
`OnnxExecutionProviderSelector.OnCandidateOptionsCreated` test-only hook.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, multi-targeted at net8.0/net9.0/net10.0
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.Tests`
- **Dependencies**: Only the base `Microsoft.ML.OnnxRuntime` CPU provider (already referenced)
  plus a small, read-only `.onnx` fixture model checked into the test project - no network access,
  no physical audio hardware, and no accelerated-provider native runtime

### Unit-Level Test Scenarios

See the unit's own verification document: _SpeechOnnx OnnxExecutionProviderSelector
Verification_ (`onnx-execution-provider-selector.md`).

### Acceptance Criteria

A SpeechOnnx OnnxRuntimeSubsystem test run passes when: a preferred provider name that constructs
successfully (the `"CPUExecutionProvider"` stand-in) is used to construct the returned session; an
unknown/unavailable provider name is abandoned in favor of the next candidate (or CPU) and its
`SessionOptions` is confirmed disposed (`IsClosed == true`); a `validateSession` probe that throws
`OnnxRuntimeException` for a candidate discards that candidate and falls back to CPU without
probing the fallback; a probe throwing any other exception type propagates to the caller instead
of being swallowed; the CPU fallback candidate always succeeds for the fixture model when no
preferred provider is supplied or all preferred providers fail; and `Create` throws
`ArgumentException`/`ArgumentNullException` for a null or empty model path before attempting any
provider.
