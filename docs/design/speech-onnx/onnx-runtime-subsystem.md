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

Stage 1 of this library's execution-provider support keeps `DefaultProviderNames` empty
(CPU-only), matching the "start simple" design decision for the raw-ONNX model family this package
supports; a later pass can populate it (for example with `"CUDAExecutionProvider"` or
`"DmlExecutionProvider"`) without changing this unit's layered try/fall-back design at all.
