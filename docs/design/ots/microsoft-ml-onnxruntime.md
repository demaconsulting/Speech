## Microsoft.ML.OnnxRuntime

### Purpose

Microsoft.ML.OnnxRuntime is used as the managed ONNX Runtime API through which the SpeechOnnx
family of libraries (`DemaConsulting.Speech.Onnx`, `.Onnx.Kokoro`, `.Onnx.NemotronStt`, and
`.Onnx.ChatterboxTurbo`) run raw `.onnx` neural-network graphs locally and offline. It was chosen
because it is the reference, maintained, permissively licensed runtime for ONNX models and ships
the CPU execution provider in the base package, so the model packages need no engine of their own.

### Features Used

- `InferenceSession` (constructed from a model path and `SessionOptions`) to load and run each
  graph
- `SessionOptions` provider-append and thread-count settings, used by the shared
  `OnnxExecutionProviderSelector` to try accelerated execution providers before falling back to
  the built-in CPU provider
- `OrtValue`/`DenseTensor<T>` and `NamedOnnxValue` input and output binding for the Kokoro,
  Nemotron, and Chatterbox Turbo graphs
- `OnnxRuntimeException`, which the provider selector distinguishes from other failures when a
  candidate provider cannot be used

### Integration Pattern

`DemaConsulting.Speech.Onnx`, `DemaConsulting.Speech.Onnx.Kokoro`,
`DemaConsulting.Speech.Onnx.NemotronStt`, and `DemaConsulting.Speech.Onnx.ChatterboxTurbo`
reference `Microsoft.ML.OnnxRuntime` directly from their project files, which are the single source
of truth for the pinned version. The CLI and Demo applications reference the same version with
native assets excluded, so that sherpa-onnx's own native runtime is the one deployed. Model packages create
sessions through `OnnxExecutionProviderSelector.Create`, which keeps provider fallback in one
place; no ONNX Runtime type appears in any public API of the Speech library, which does not
reference the package at all.
