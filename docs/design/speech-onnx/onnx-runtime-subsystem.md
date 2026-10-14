## OnnxRuntimeSubsystem Design

![OnnxRuntimeSubsystem Structure](OnnxRuntimeSubsystemView.svg)

### Overview

The SpeechOnnx OnnxRuntimeSubsystem supplies the library's only generic ONNX Runtime
infrastructure: opportunistic, GPU-then-CPU-fallback execution-provider selection for constructing
an ONNX Runtime `InferenceSession`. It contains the following unit:

- **OnnxExecutionProviderSelector**: a static helper that builds an `InferenceSession` for a
  given `.onnx` model path, trying each of a caller-supplied ordered list of preferred execution
  provider names before falling back to the always-available CPU provider

### Interfaces

The subsystem exposes `OnnxExecutionProviderSelector`'s `DefaultProviderNames` property and
`Create` method as its entire public API. It consumes the `Microsoft.ML.OnnxRuntime` managed API
(`InferenceSession`, `SessionOptions`, `OnnxRuntimeException`). It is consumed by every sibling
model package's own engine-construction code (for example a model's `CreateBackend`
implementation), each of which supplies its own `.onnx` model path and its own ordered list of
preferred provider names.

### Design

`OnnxExecutionProviderSelector.Create` is this subsystem's only member. Given a model path and an
optional ordered list of preferred accelerated provider names (falling back to the empty
`DefaultProviderNames` when `null`), it iterates the candidates in order: for each, it constructs a
fresh `SessionOptions`, calls `AppendExecutionProvider(providerName)` on it, and attempts
`new InferenceSession(modelPath, options)`. The `SessionOptions` instance is wrapped in a `using`
statement scoped to each loop iteration, so it is disposed immediately after that iteration's
construction attempt completes - whether the attempt throws or succeeds - without disposing the
`InferenceSession` the method ultimately returns. A candidate that throws `OnnxRuntimeException` or
`DllNotFoundException` (its native shared library is not available in this process) is abandoned
and the next candidate is tried with its own fresh `SessionOptions`, so one candidate's failure can
never leave partially-applied configuration behind for the next. If no preferred candidate
succeeds, the method constructs `new InferenceSession(modelPath)` with no explicit provider, which
uses the CPU provider built into the base package and is guaranteed to succeed for a well-formed
model - this final candidate never throws due to an unavailable accelerated provider, since it
requires none.

`DefaultProviderNames` is empty (CPU-only), matching the "start simple" design decision for the
raw-ONNX model family this package supports. A model package opts in to acceleration by passing its
own ordered provider list (for example `"CUDAExecutionProvider"` or `"DmlExecutionProvider"`)
without changing this unit's layered try/fall-back design at all.

### Error Handling

The subsystem's exception contract is the same as the unit's: only a provider's _unavailability_
(an `OnnxRuntimeException` or `DllNotFoundException` from a candidate's construction or probe) is
swallowed into the CPU fallback. A null or empty model path is reported as `ArgumentException`
(`ArgumentNullException` for null) before any provider is attempted; a missing, unreadable, or
malformed model is reported as `OnnxRuntimeException` from the final CPU attempt; and any
unexpected exception from the caller's `validateSession` probe propagates after the candidate's
session is disposed. See _SpeechOnnx OnnxExecutionProviderSelector Design_.

### Test Isolation

`OnnxExecutionProviderSelector` exposes a process-wide static, internal, test-only hook
(`OnCandidateOptionsCreated`). The test project serializes every test that calls `Create` in one
xUnit collection with parallelization disabled, and installs the hook only through a disposable
scope that restores the previously installed hook, so the hook can never leak between tests.
