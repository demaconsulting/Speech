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
  test project (the malformed-model test writes and deletes its own temporary file); no network
  access, no physical accelerator hardware, and no external services are required. Because
  `OnCandidateOptionsCreated` is process-wide static state, the whole test class runs in the xUnit
  collection `OnnxSelectorHookCollection` (`DisableParallelization = true`), and tests install the
  hook only through the disposable `SelectorHookScope`, which restores the previously installed
  hook on `Dispose` (itself verified by `SelectorHookScope_Dispose_RestoresPreviousHook`)

#### Acceptance Criteria

A null or empty model path throws `ArgumentException` (`ArgumentNullException` for null) naming
`modelPath` before any provider is attempted; calling
`Create` with no preferred providers returns a working CPU-backed session that can run the
fixture model; an unknown/unavailable provider name fails to construct and `Create` falls back to
a working CPU session instead of throwing; a `validateSession` probe that throws
`OnnxRuntimeException` for a candidate causes that candidate to be discarded (its session
disposed) and the CPU fallback used instead, with the CPU fallback itself never passed to the
probe; a probe throwing `DllNotFoundException` is treated the same way; a probe throwing any other
exception type propagates to the caller unchanged, after the candidate's session is disposed,
instead of being treated as "try the next candidate"; a missing or malformed model file throws
`OnnxRuntimeException` to the caller even when a preferred provider was supplied; and
every candidate's `SessionOptions` instance - both a failed candidate's and the eventually
winning candidate's - is disposed (`IsClosed == true`) by the time `Create` returns; candidates are
tried in the supplied order and the first one accepted (a probe-passing session) is the one
returned; and that returned session is not disposed and remains usable for inference.

#### Test Scenarios

##### A null model path throws ArgumentNullException

**Test**: `OnnxExecutionProviderSelector_Create_NullModelPath_ThrowsArgumentNullException`

##### An empty model path throws ArgumentException

**Test**: `OnnxExecutionProviderSelector_Create_EmptyModelPath_ThrowsArgumentException`

##### No preferred providers creates a working CPU session

**Test**: `OnnxExecutionProviderSelector_Create_NoPreferredProviders_CreatesWorkingCpuSession`

##### An unknown/unavailable provider name falls back to a working CPU session

**Test**: `OnnxExecutionProviderSelector_Create_UnknownProviderName_FallsBackToCpuSession`

##### A validateSession probe throwing OnnxRuntimeException discards the candidate without probing the CPU fallback

**Test**: `OnnxExecutionProviderSelector_Create_ValidateSessionProbeThrowsOnnxRuntimeException_DisposesCandidateAndFallsBackToCpuWithoutProbingFallback`

##### A validateSession probe throwing a non-ORT exception propagates to the caller and the candidate is disposed

**Test**: `OnnxExecutionProviderSelector_Create_ValidateSessionProbeThrowsOtherException_RethrowsAndDisposesCandidateSession`

Error scenario: the probe throws `InvalidOperationException` for the first of two candidates. The
same exception instance reaches the caller, the second candidate is never tried, and the first
candidate's session is unusable (disposed).

##### A validateSession probe throwing DllNotFoundException is treated as an unavailable provider

**Test**: `OnnxExecutionProviderSelector_Create_ValidateSessionProbeThrowsDllNotFoundException_FallsBackToCpuSession`

##### A missing model file propagates OnnxRuntimeException

**Test**: `OnnxExecutionProviderSelector_Create_MissingModelFile_PropagatesOnnxRuntimeException`

Error scenario: a path to a file that does not exist reports the ONNX Runtime error from the
final CPU attempt.

##### A malformed model file propagates OnnxRuntimeException even with a preferred provider

**Test**: `OnnxExecutionProviderSelector_Create_MalformedModelFileWithPreferredProvider_PropagatesOnnxRuntimeException`

Error/boundary scenario: the candidate's failure is only "provider unavailable"; the genuine model
error is reported by the CPU attempt.

##### The hook scope restores the previously installed hook

**Test**: `SelectorHookScope_Dispose_RestoresPreviousHook`

Test-infrastructure scenario proving the isolation helper described under Test Environment.

##### Every candidate's SessionOptions instance is disposed, win or lose

**Test**: `OnnxExecutionProviderSelector_Create_MultipleCandidates_DisposesEveryCandidateOptionsInstance`

##### The CPU fallback is never probed when no preferred providers are supplied

**Test**: `OnnxExecutionProviderSelector_Create_EmptyPreferredProviders_NeverInvokesProbe`

##### Candidates are tried in order and the first accepted candidate's session is returned

**Test**: `OnnxExecutionProviderSelector_Create_FirstCandidateFailsProbe_ReturnsSecondCandidateSession`

##### A candidate whose probe succeeds is the session returned to the caller

**Test**: `OnnxExecutionProviderSelector_Create_ValidateSessionProbeSucceeds_ReturnsProbedCandidateSession`

##### The returned session is not disposed and remains usable for inference

**Test**: `OnnxExecutionProviderSelector_Create_Success_ReturnedSessionRemainsUsableForInference`
