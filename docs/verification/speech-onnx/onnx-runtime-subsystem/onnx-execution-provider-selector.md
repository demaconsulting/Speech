### OnnxExecutionProviderSelector Verification

#### Verification Approach

`OnnxExecutionProviderSelector` has **no automated test coverage today**. There is no
`test/DemaConsulting.Speech.Onnx.Tests` directory anywhere in this repository, and this unit has
never been exercised by an automated test of any kind - not a real-provider integration test, not
a mocked/stubbed unit test. This is a known, pre-existing gap, honestly recorded here rather than
closed by inventing a fictitious test name. Correctness of the unit's current, fixed behavior -
every candidate `SessionOptions` disposed via `using` immediately after its own `InferenceSession`
construction attempt, win or lose, without disposing the returned `InferenceSession` on success -
rests today solely on code review evidence: the resource-leak fix was reviewed directly against
the source during this pass, confirming each loop iteration's `SessionOptions` is now
deterministically disposed regardless of outcome, and that the returned session is left
untouched for the caller to own.

The scenarios below describe the behavior a future unit test suite should prove once a test
project exists. They would not require mocking the ONNX Runtime API itself - `InferenceSession`
and `SessionOptions` are concrete sealed-enough types best exercised directly against a small,
real, valid `.onnx` fixture model and, for the accelerated-provider scenarios, a deliberately
unavailable provider name (which fails identically whether or not the test machine has a real
accelerator installed, since the method's own fallback path is what is under test, not any
specific accelerator's correctness).

#### Test Environment

No automated test environment exists yet. A future test project would run under xUnit v3 via
`dotnet test`, requiring only the base `Microsoft.ML.OnnxRuntime` CPU provider (already
referenced by this package) and a small valid `.onnx` fixture model checked into the test
project - no network access, no physical audio hardware, and no external services.

#### Acceptance Criteria

Not yet measurable by automated means; see Verification Approach. Once a test project exists,
the unit should be considered verified when: a preferred provider name that constructs
successfully is used without falling through to the next candidate or to CPU; a preferred
provider name whose native runtime is unavailable causes that candidate's `SessionOptions` to be
disposed and the next candidate (or CPU) to be tried; omitting preferred providers entirely (or
every preferred candidate failing) returns a CPU-backed session without throwing; and `Create`
throws `ArgumentException` for a null or empty model path before any provider is attempted.

#### Test Scenarios

The following scenarios are not yet automated (see Verification Approach above); they are
recorded here as the intended coverage for when `test/DemaConsulting.Speech.Onnx.Tests` is
created:

##### A successfully-constructing preferred provider is used without falling back

Verifies that when the first preferred provider name constructs a real `InferenceSession`
successfully, that session is returned and no further candidate (including CPU) is attempted.

##### An unavailable preferred provider falls through to the next candidate

Verifies that a preferred provider name whose native runtime is unavailable in the test process
(raising `OnnxRuntimeException` or `DllNotFoundException`) is abandoned, its `SessionOptions` is
disposed, and the next candidate in the ordered list is tried with a fresh `SessionOptions`.

##### Every preferred candidate failing falls back to the CPU provider

Verifies that when every preferred provider name fails, `Create` still returns a loaded
`InferenceSession` using the default (CPU) provider, without throwing.

##### No preferred providers supplied falls back to the CPU provider

Verifies that calling `Create` with `preferredProviderNames` left `null` (resolving to the empty
`DefaultProviderNames`) returns a CPU-backed session directly, with no candidate loop iterations.

##### A null or empty model path throws ArgumentException

Verifies that `Create` throws `ArgumentException` for a null or empty `modelPath`, before
attempting to construct any `SessionOptions` or `InferenceSession`.
