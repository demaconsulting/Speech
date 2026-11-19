## Microsoft.ML.OnnxRuntime

### Purpose

Microsoft.ML.OnnxRuntime is used as the managed ONNX Runtime API through which the SpeechOnnx
family of libraries (`DemaConsulting.Speech.Onnx`, `.Onnx.Kokoro`, `.Onnx.Piper`, `.Onnx.NemotronStt`, and
`.Onnx.ChatterboxTurbo`) run raw `.onnx` neural-network graphs locally and offline. It was chosen
because it is the reference, maintained, permissively licensed runtime for ONNX models and ships
the CPU execution provider in the base package, so the model packages need no engine of their own.

### Features Used

- `InferenceSession` (constructed from a model path and `SessionOptions`) to load and run each
  graph
- `SessionOptions` provider-append and thread-count settings, used by the shared
  `OnnxExecutionProviderSelector` to try accelerated execution providers before falling back to
  the built-in CPU provider
- `OrtValue`/`DenseTensor<T>` and `NamedOnnxValue` input and output binding for the Kokoro, Piper,
  Nemotron, and Chatterbox Turbo graphs
- `OnnxRuntimeException`, which the provider selector distinguishes from other failures when a
  candidate provider cannot be used

### Integration Pattern

`DemaConsulting.Speech.Onnx`, `DemaConsulting.Speech.Onnx.Kokoro`, `DemaConsulting.Speech.Onnx.Piper`,
`DemaConsulting.Speech.Onnx.NemotronStt`, and `DemaConsulting.Speech.Onnx.ChatterboxTurbo`
reference `Microsoft.ML.OnnxRuntime` directly from their project files, which are the single source
of truth for the version floor (1.28.0, the lowest release they are built and tested against). The libraries rely
only on the ONNX operator sets and model format supported by that release and use no managed API newer than it;
the graphs they load are produced offline by their publishers. A consumer may reference a newer
`Microsoft.ML.OnnxRuntime`, but the managed version must never be newer than the native `onnxruntime` library
that ends up loaded in the process (the managed API throws when the native library is older than it).
The CLI and Demo applications reference `Microsoft.ML.OnnxRuntime` 1.28.0 directly with
`ExcludeAssets="native"`: sherpa-onnx's own copy of the native library (1.28.2, same file name) must be the
one that is deployed, because sherpa-onnx's Linux libraries are linked against its versioned symbols and fail to
load against any Microsoft build (see the SpeechCli and SpeechDemo designs). Model packages create
sessions through `OnnxExecutionProviderSelector.Create`, which keeps provider fallback in one
place; no ONNX Runtime type appears in any public API of the Speech library, which does not
reference the package at all.

Disposal: `InferenceSession` and `SessionOptions` wrap native handles and must be disposed.
`OnnxExecutionProviderSelector.Create` disposes its `SessionOptions` and every rejected candidate
session itself, and returns only the winning session, whose ownership transfers to the caller; each
network wrapper owns and disposes its sessions, and the `OrtValue` instances created for a run are
disposed when that run completes. Tests that create sessions dispose them for the same reason.
