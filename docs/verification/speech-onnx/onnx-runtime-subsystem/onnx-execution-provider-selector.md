### OnnxExecutionProviderSelector Verification

#### Verification Approach

`OnnxExecutionProviderSelector` is verified by automated unit tests in
`test/DemaConsulting.Speech.Onnx.Tests` run against a tiny, hand-built, valid single-node
`.onnx` fixture model (`TestData/tiny-identity-model.onnx`, generated offline with the Python
`onnx`/`onnxruntime` packages and never shipped in production). No accelerated execution
provider (CUDA, DirectML, etc.) can be genuinely exercised in this test project or in CI, since
this package references only the base, CPU-only `Microsoft.ML.OnnxRuntime` package (see the
class's own remarks); the tests therefore use the always-available, explicitly-named
`"CPUExecutionProvider"` string as a stand-in "candidate" to reach the exact same
construct/probe/dispose/fall-back code paths a real accelerated candidate would exercise, and a
syntactically-invalid provider name to reach the "construction itself fails" path. One claim -
that every candidate's `SessionOptions` is disposed, win or lose - is otherwise unobservable from
outside the class because `InferenceSession` never exposes or re-disposes the options instance
passed to it; the test project observes it anyway via the internal
`OnnxExecutionProviderSelector.OnCandidateOptionsCreated` test-only hook, which hands the test
each candidate's own `SessionOptions` instance (itself a `System.Runtime.InteropServices.SafeHandle`)
so the test can assert its public, inherited `IsClosed` property directly, with no reflection
into ONNX Runtime internals required.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, multi-targeted at net8.0/net9.0/net10.0
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.Tests`
- **Isolation**: every test uses the same small, read-only `.onnx` fixture file checked into the
  test project; no network access, no physical accelerator hardware, and no external services are
  required

#### Acceptance Criteria

A null or empty model path throws `ArgumentException` before any provider is attempted; calling
`Create` with no preferred providers returns a working CPU-backed session that can run the
fixture model; an unknown/unavailable provider name fails to construct and `Create` falls back to
a working CPU session instead of throwing; a `validateSession` probe that throws
`OnnxRuntimeException` for a candidate causes that candidate to be discarded and the CPU fallback
used instead, with the CPU fallback itself never passed to the probe; a probe throwing any other
exception type propagates to the caller instead of being treated as "try the next candidate"; and
every candidate's `SessionOptions` instance - both a failed candidate's and the eventually
winning candidate's - is disposed (`IsClosed == true`) by the time `Create` returns.

#### Test Scenarios

##### A null model path throws ArgumentNullException

**Test**: `Create_NullModelPath_ThrowsArgumentNullException`

##### An empty model path throws ArgumentException

**Test**: `Create_EmptyModelPath_ThrowsArgumentException`

##### No preferred providers creates a working CPU session

**Test**: `Create_NoPreferredProviders_CreatesWorkingCpuSession`

##### An unknown/unavailable provider name falls back to a working CPU session

**Test**: `Create_UnknownProviderName_FallsBackToCpuSession`

##### A validateSession probe throwing OnnxRuntimeException discards the candidate without probing the CPU fallback

**Test**: `Create_ValidateSessionProbeThrowsOnnxRuntimeException_DisposesCandidateAndFallsBackToCpuWithoutProbingFallback`

##### A validateSession probe throwing a non-ORT exception propagates to the caller

**Test**: `Create_ValidateSessionProbeThrowsOtherException_Rethrows`

##### Every candidate's SessionOptions instance is disposed, win or lose

**Test**: `Create_MultipleCandidates_DisposesEveryCandidateOptionsInstance`

##### The CPU fallback is never probed when no preferred providers are supplied

**Test**: `Create_EmptyPreferredProviders_NeverInvokesProbe`
