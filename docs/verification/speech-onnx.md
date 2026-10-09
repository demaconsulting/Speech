# SpeechOnnx System Verification Design

This document describes the system-level verification strategy for the SpeechOnnx library.

## Verification Approach

SpeechOnnx has **no automated test project today**: there is no
`test/DemaConsulting.Speech.Onnx.Tests` directory anywhere in this repository, and its one
subsystem and one unit, `OnnxExecutionProviderSelector`, have never been exercised by an
automated test. This is a known, pre-existing gap recorded honestly here, not a design decision
and not something this document glosses over with an invented test name. Correctness of the
unit's current, fixed resource-management contract - every candidate `SessionOptions` is disposed
immediately after its own `InferenceSession` construction attempt, win or lose, and every
candidate's `InferenceSession` is disposed on every non-winning path (including an unexpected
exception type from a caller-supplied probe), without disposing the returned `InferenceSession`
itself on success - and of the caller-supplied probe contract (`validateSession`, run on each
accelerated candidate session before it is accepted; the CPU fallback is never probed) - rests
today solely on code review evidence (these changes were reviewed directly against the source),
not on executed test evidence.

This document still describes, at design level, the test scenarios a future
`test/DemaConsulting.Speech.Onnx.Tests` project should implement, so that once that project
exists, the requirement ids in `docs/reqstream/speech-onnx/**/*.yaml` already have a destination
`tests:` entry to carry. See _SpeechOnnx OnnxRuntimeSubsystem Verification_ for the subsystem-level
detail and _SpeechOnnx OnnxExecutionProviderSelector Verification_ for the unit-level detail.

## Test Environment

No automated test environment exists yet for this system. A future test project would run under
xUnit v3 via `dotnet test`, matching every other system in this repository, and would need no
network access, no physical audio hardware, and no external services - only the base
`Microsoft.ML.OnnxRuntime` CPU execution provider, which this package already references, plus a
small valid `.onnx` fixture model for constructing real `InferenceSession` instances. Scenarios
that assert accelerated-provider fallback behavior would additionally depend on whether a given
provider's native runtime is present on the test machine, and should self-skip or assert the
fallback path accordingly rather than require a specific accelerator to be installed in CI.

## Acceptance Criteria

Not yet measurable by automated means. Until `test/DemaConsulting.Speech.Onnx.Tests` exists, this
system's correctness rests entirely on manual code review of `OnnxExecutionProviderSelector`,
most recently the review that confirmed each candidate `SessionOptions` is wrapped in a `using`
statement and disposed after each construction attempt, each candidate's `InferenceSession` is
disposed in a `try`/`finally` on every path except the one that returns it (including any
exception type the probe or construction might throw, not only the two expected "try next
candidate" types), and `validateSession` is invoked only on an accelerated candidate, never on
the CPU fallback. Once an automated test project exists, a passing system-level test run should
require: a preferred provider that successfully constructs a session and passes its probe (if
any) is used without falling through to CPU; a preferred provider whose native runtime is
unavailable, or whose probe throws `OnnxRuntimeException`/`DllNotFoundException`, is abandoned in
favor of the next candidate (or CPU) without leaking its `SessionOptions` or `InferenceSession`;
an unexpected exception type from a candidate's construction or probe propagates to the caller
after that candidate's session is disposed, rather than being swallowed; the CPU fallback always
succeeds for a well-formed model and is never probed; and a null or empty model path throws
`ArgumentException` before any `SessionOptions` is constructed.

## Test Scenarios

The following scenarios are not yet automated (see Verification Approach); they describe the
design-level behavior a future test project should prove, decomposed further by subsystem and
unit in _SpeechOnnx OnnxRuntimeSubsystem Verification_ and _SpeechOnnx
OnnxExecutionProviderSelector Verification_:

- **Provider-fallback end-to-end**: constructing a session through
  `OnnxExecutionProviderSelector.Create` with a mix of available and unavailable preferred
  providers, confirming the first successfully-constructed (and, if probed, successfully
  probed) candidate's session is returned and no `SessionOptions` or discarded candidate
  `InferenceSession` is left undisposed
- **Probe-driven fallback**: constructing a session with a `validateSession` probe that throws
  `OnnxRuntimeException` for an accelerated candidate that loads successfully but cannot run the
  model (the DirectML/`ConvTranspose` scenario this parameter was added for), confirming that
  candidate's session is disposed and the next candidate (or CPU) is tried, and that the CPU
  fallback itself is never passed to `validateSession`
- **Any-exception disposal**: constructing a session where a candidate's construction or probe
  throws an exception type other than `OnnxRuntimeException`/`DllNotFoundException`, confirming
  that candidate's session is disposed before the exception propagates to the caller, rather than
  being swallowed as a "try next candidate" signal
- **CPU-only fallback**: constructing a session with no preferred providers (or
  `DefaultProviderNames`, currently empty) returns a CPU-backed session without throwing
