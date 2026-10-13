### SynthesisBackend (ISynthesisBackend, ISynthesisBackendFactory, DefaultSynthesisBackendFactory)

**Purpose**: Confine every speech-synthesis interop call behind one mockable boundary, so the
session's chunking, Layer 2 rendering, per-segment synthesis, and fault-containment logic is
verifiable with pure managed fakes - no downloaded model and no platform-specific native binary
are ever required in CI. This mirrors the `IRecognitionBackend`/`IRecognitionBackendFactory` seam
used for recognition. The "engine" vocabulary is reserved for the public Layer 3
`ISpeechSynthesizerEngine` contract. `DefaultSynthesisBackendFactory` is the one real
`ISynthesisBackendFactory` implementation: it holds no engine-specific knowledge at all, because
the design makes each model's backing class responsible for constructing its own backend, so
adding a synthesis model, or an entire model-supplying extension package, never requires changing
this factory.

**Data Model**: The two interfaces carry no data. `DefaultSynthesisBackendFactory` is stateless
and has no fields.

**Key Methods**:

- **ISynthesisBackend.SampleRate**: The fixed rate this backend's `Generate(...)` output is
  produced at.
- **ISynthesisBackend.Generate(text, speed, speakerId)**: Synthesizes one chunk of text at the
  given speed multiplier and speaker id, returning an `BackendAudio` (samples plus the rate they
  were produced at).
- **ISynthesisBackendFactory.Create(ISynthesisModel, string installedModelDirectory)**: Loads a
  backend for the model.
- **DefaultSynthesisBackendFactory.Create(ISynthesisModel, string installedModelDirectory)**:
  Validates its arguments and forwards to the model's own public
  `ISynthesisModel.CreateBackend(installedModelDirectory)`, returning the loaded backend
  unchanged.

**Error Handling**: Implementations of `ISynthesisBackend` are not thread-safe by contract; a
session calls `Generate` from exactly one `DedicatedWorker` call at a time. Load failures surface
as exceptions from `Create(...)`, which `SpeechSynthesizerFactory` converts into the honest
unavailable fallback. `DefaultSynthesisBackendFactory.Create` throws `ArgumentNullException` for
a null model and `ArgumentException` for a null or empty directory; any exception the model's
`CreateBackend` raises - for example a missing native runtime or unusable model files - propagates
unchanged.

**Dependencies**: `BackendAudio`; `ISynthesisModel` from the ModelManagementSubsystem.

**Callers**: `SynthesisSession` (backend) and `SpeechSynthesizerFactory`/`SpeechSynthesizerEngine`
(factory; the public `LoadAsync(...)` overloads reach `DefaultSynthesisBackendFactory`).

The real sherpa-onnx `ISynthesisBackend` implementation, `SherpaOnnxSynthesisEngine`, is no longer
part of this subsystem: it moved, together with the concrete sherpa-onnx models that construct it,
to the sibling `SpeechSherpa` system. See _SherpaOnnxSynthesisEngine_ in _SpeechSherpa
SynthesisSubsystem Design_.
