# SpeechOnnx System Verification Design

This document describes the system-level verification strategy for the SpeechOnnx library.

## Verification Approach

SpeechOnnx is verified at system level through its one subsystem and one unit,
`OnnxExecutionProviderSelector`, exercised against a tiny, hand-built, valid `.onnx` fixture model
rather than any real accelerated execution provider: no CUDA, DirectML, or other accelerated
native runtime can be genuinely exercised by this package, since it references only the base,
CPU-only `Microsoft.ML.OnnxRuntime` package. The tests therefore use the always-available,
explicitly-named `"CPUExecutionProvider"` string as a stand-in "candidate" to reach the exact same
construct/probe/dispose/fall-back code paths a real accelerated candidate would exercise, and a
syntactically-invalid provider name to reach the "construction itself fails" path. This proves the
unit's fixed resource-management contract - every candidate `SessionOptions` is disposed
immediately after its own `InferenceSession` construction attempt, win or lose, and every
candidate's `InferenceSession` is disposed on every non-winning path (including an unexpected
exception type from a caller-supplied probe), without disposing the returned `InferenceSession`
itself on success - and the caller-supplied probe contract (`validateSession`, run on each
accelerated candidate session before it is accepted; the CPU fallback is never probed).

Automated coverage **does not** extend to a genuinely accelerated execution provider (CUDA,
DirectML, etc.), since this package's own dependency set cannot load one; that remains the
responsibility of each consuming model package (for example the sibling
`DemaConsulting.Speech.Onnx.Kokoro` package) to verify against its own real installed model and
runtime.

System and unit tests reside in the `test/DemaConsulting.Speech.Onnx.Tests` project. See
_SpeechOnnx OnnxRuntimeSubsystem Verification_ for the subsystem-level detail and _SpeechOnnx
OnnxExecutionProviderSelector Verification_ for the unit-level detail.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, multi-targeted at net8.0/net9.0/net10.0
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.Tests`
- **Dependencies**: No external services, no network access, and no physical audio or accelerator
  hardware - only the base `Microsoft.ML.OnnxRuntime` CPU execution provider this package already
  references, plus a small, read-only `.onnx` fixture model checked into the test project

## External Interface Simulation

The tests use no mocking framework: `OnnxExecutionProviderSelector.Create` is exercised directly
against a real `InferenceSession` built from a tiny, hand-built, valid single-node `.onnx` fixture
model (`TestData/tiny-identity-model.onnx`, generated offline with the Python
`onnx`/`onnxruntime` packages and never shipped in production). One claim - that every candidate's
`SessionOptions` is disposed, win or lose - is otherwise unobservable from outside the class
because `InferenceSession` never exposes or re-disposes the options instance passed to it; the
test project observes it anyway via the internal
`OnnxExecutionProviderSelector.OnCandidateOptionsCreated` test-only hook, which hands the test
each candidate's own `SessionOptions` instance so the test can assert its public, inherited
`IsClosed` property directly, with no reflection into ONNX Runtime internals required.

## System-Level Test Scenarios

### No preferred providers creates a working CPU session

**Test**: `Create_NoPreferredProviders_CreatesWorkingCpuSession`

Verifies that calling `Create` with no preferred providers returns a working CPU-backed session
that can run the fixture model.

### An unknown/unavailable provider name falls back to a working CPU session

**Test**: `Create_UnknownProviderName_FallsBackToCpuSession`

Verifies that a candidate provider name that fails to construct does not throw, and `Create`
falls back to a working CPU session instead.

### A validateSession probe throwing OnnxRuntimeException discards the candidate without probing the CPU fallback

**Test**: `Create_ValidateSessionProbeThrowsOnnxRuntimeException_DisposesCandidateAndFallsBackToCpuWithoutProbingFallback`

Verifies the DirectML/`ConvTranspose`-style scenario this parameter was added for: a
`validateSession` probe that throws `OnnxRuntimeException` for an accelerated candidate causes
that candidate's session to be disposed and discarded, the next candidate (or CPU) to be tried,
and the CPU fallback itself to never be passed to `validateSession`.

### A validateSession probe throwing a non-ORT exception propagates to the caller

**Test**: `Create_ValidateSessionProbeThrowsOtherException_Rethrows`

Verifies that a probe throwing any exception type other than
`OnnxRuntimeException`/`DllNotFoundException` propagates to the caller instead of being treated
as a "try the next candidate" signal, after that candidate's session is disposed.

## Acceptance Criteria

A SpeechOnnx system-level test run passes when all scenarios above pass without unexpected
exceptions, every subsystem-level suite passes (see _SpeechOnnx OnnxRuntimeSubsystem
Verification_ and _SpeechOnnx OnnxExecutionProviderSelector Verification_ for the full set,
including the `SessionOptions`-disposal and argument-validation scenarios), and the automated
verification boundary remains honest: provider-candidate construction, probing, disposal, and
CPU fallback against the base CPU execution provider are claimed as automated coverage, while
behavior against a genuinely accelerated execution provider is left to each consuming model
package's own verification.
