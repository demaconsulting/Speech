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

**Data Model**: The interfaces carry no data. `DefaultSynthesisBackendFactory` is stateless
and has no fields. `IAnnotatedSynthesisBackend` is a public interface that extends
`ISynthesisBackend` with one additional method; a backend implements it only to receive
annotations, and a legacy backend that implements only `ISynthesisBackend` is unchanged and never
sees annotations. It is a separate derived interface rather than a default interface member of
`ISynthesisBackend` so that hand-written fakes and mocking libraries see no ambiguity.

**Key Methods**:

- **ISynthesisBackend.SampleRate**: The fixed rate this backend's `Generate(...)` output is
  produced at.
- **ISynthesisBackend.Generate(text, speed, speakerId)**: Synthesizes one chunk of text at the
  given speed multiplier and speaker id, returning an `BackendAudio` (samples plus the rate they
  were produced at).
- **IAnnotatedSynthesisBackend.GenerateAnnotated(text, annotations, speed, speakerId)**:
  Synthesizes one chunk of text together with the `SegmentAnnotation`s the text analysis produced
  for it. The session calls it **instead of** `Generate`, and only when the segment carries at
  least one annotation and the backend implements this interface. Contract: `annotations` is
  non-null and non-empty, every annotation lies within `[0, text.Length]`, and offsets are
  relative to `text`. A backend must ignore annotation kinds it does not recognize and must
  produce audio for `text` even when it ignores every annotation, so the result is then equivalent
  to `Generate`. Threading and disposal rules are inherited from `ISynthesisBackend`.
- **ISynthesisBackendFactory.Create(ISynthesisModel, string installedModelDirectory)**: Loads a
  backend for the model.
- **DefaultSynthesisBackendFactory.Create(ISynthesisModel, string installedModelDirectory)**:
  Validates its arguments and forwards to the model's own public
  `ISynthesisModel.CreateBackend(installedModelDirectory)`, returning the loaded backend
  unchanged.

**Error Handling**: Implementations of `ISynthesisBackend` are not thread-safe by contract; a
session calls `Generate` or `GenerateAnnotated` from exactly one `DedicatedWorker` call at a time.
An exception from `GenerateAnnotated` is handled exactly like one from `Generate` (the session
faults and reports it). Load failures surface
as exceptions from `Create(...)`, which `SpeechSynthesizerFactory` converts into the honest
unavailable fallback. `DefaultSynthesisBackendFactory.Create` throws `ArgumentNullException` for
a null model and `ArgumentException` for a null or empty directory; any exception the model's
`CreateBackend` raises - for example a missing native runtime or unusable model files - propagates
unchanged.

**Dependencies**: `BackendAudio` and `SegmentAnnotation` (for `IAnnotatedSynthesisBackend`) from
this subsystem; `ISynthesisModel` from the ModelManagementSubsystem.

**Callers**: `SynthesisSession` (backend, including the annotated dispatch) and
`SpeechSynthesizerFactory`/`SpeechSynthesizerEngine`
(factory; the public `LoadAsync(...)` overloads reach `DefaultSynthesisBackendFactory`).

The real sherpa-onnx `ISynthesisBackend` implementation, `SherpaOnnxSynthesisEngine`, is no longer
part of this subsystem: it moved, together with the concrete sherpa-onnx models that construct it,
to the sibling `SpeechSherpa` system. See _SherpaOnnxSynthesisEngine_ in _SpeechSherpa
SynthesisSubsystem Design_.
