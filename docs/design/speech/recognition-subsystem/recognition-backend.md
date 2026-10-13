### RecognitionBackend

**Purpose**: Confine every speech-inference interop call behind one mockable boundary
(`IRecognitionBackend` and its internal `IRecognitionBackendFactory`), so the session's threading,
conversion, and result-delivery logic is verifiable with pure managed fakes - no downloaded model
and no platform-specific native binary are ever required in CI. This mirrors the `IPortAudioApi`
seam used for audio interop and the `IModelDownloadClient` seam used for downloads. The unit also
includes `DefaultRecognitionBackendFactory`, the one real factory implementation, which holds no
engine-specific knowledge at all: each model's backing class is responsible for constructing its
own backend, so adding a model, or an entire model-supplying extension package, never requires
changing the factory.

**Data Model**: The two seam types are interfaces only. `DefaultRecognitionBackendFactory` is
stateless; it has no fields.

**Key Methods**:

- **IRecognitionBackend.AcceptSamples(`ReadOnlySpan<float>`)**: Buffers one block of mono
  samples, already at the model's required rate, into the current utterance. Never blocks on
  decoding; an empty block is a no-op.
- **IRecognitionBackend.TryDecode(out SpeechRecognitionResult?)**: Decodes as much buffered audio
  as possible and reports the next result, returning `false` when there is nothing new. Reporting
  "nothing new" instead of an empty result keeps the caller's event stream free of duplicates
  while silence is streaming. Callers may poll until it returns `false`.
- **IRecognitionBackend.TryFlush(out SpeechRecognitionResult?)**: Finalizes and decodes any
  buffered-but-undecoded audio - the tail of an utterance released with no trailing silence, which
  `TryDecode` alone cannot decode without future context that will now never arrive - and reports
  it as one last final result if it produced non-empty text, or `false` if there was nothing to
  recover. Called once, at session end, immediately before `Reset()`.
- **IRecognitionBackend.Reset()**: Discards a partially decoded utterance.
- **IRecognitionBackendFactory.Create(IRecognitionModel, string installedModelDirectory,
  parameterValues)**: Loads a backend for the model; the real implementation asks the model to
  construct its own backend via the public `IRecognitionModel.CreateBackend`.
- **DefaultRecognitionBackendFactory.Create(IRecognitionModel, string installedModelDirectory,
  `IReadOnlyDictionary<string, object>?` parameterValues)**: Validates its arguments and
  forwards to the model's own public
  `IRecognitionModel.CreateBackend(installedModelDirectory, parameterValues)`, returning the
  loaded backend unchanged. It replaced the former `SherpaOnnxRecognitionEngineFactory`.

**Error Handling**: Implementations are not thread-safe by contract; the session calls them from
exactly one pump thread. Load failures surface as exceptions from `Create(...)`, which
`SpeechRecognizerFactory` converts into the honest unavailable fallback.
`DefaultRecognitionBackendFactory.Create` throws `ArgumentNullException` for a null model and
`ArgumentException` for a null or empty directory; any exception the model's `CreateBackend`
raises - for example a missing native runtime or unusable model files - propagates unchanged.

**Dependencies**: `SpeechRecognitionResult`; `IRecognitionModel` from the
ModelManagementSubsystem.

**Callers**: `RecognitionSession` (backend) and `SpeechRecognizerFactory` (factory, including the
public `LoadAsync(...)` overloads that reach `DefaultRecognitionBackendFactory`).

The real sherpa-onnx `IRecognitionBackend` implementation, `SherpaOnnxRecognitionEngine` -
including its post-endpoint warm-up replay mitigation - is no longer part of this subsystem: it
moved, together with the concrete sherpa-onnx models that construct it, to the sibling
`SpeechSherpa` system. See *SherpaOnnxRecognitionEngine* in *SpeechSherpa RecognitionSubsystem
Design*.
