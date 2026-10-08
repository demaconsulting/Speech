## SynthesisSubsystem Design

![SynthesisSubsystem Structure](SherpaSynthesisSubsystemView.svg)

### Overview

The SpeechSherpa SynthesisSubsystem supplies the real, sherpa-onnx-backed implementation of the
core `Speech` library's public `ISynthesisBackend` seam (see _Speech SynthesisSubsystem Design_).
It previously lived inside the core library's own SynthesisSubsystem; it moved here, unchanged in
behavior, together with the models that construct it, so the core library carries no sherpa-onnx
dependency at all. The core library's session, engine, Natural Language Audio Tag rendering,
chunking, resampling, and composition logic remain in the core library and drive this backend
only through `ISynthesisBackend`. It contains the following unit:

- **SherpaOnnxSynthesisEngine**: the real `ISynthesisBackend` over sherpa-onnx's offline
  `OfflineTts` API

### Interfaces

The subsystem exposes no public API: `SherpaOnnxSynthesisEngine` is an `internal` class that
implements the core library's public `ISynthesisBackend` (`SampleRate`, `Generate`, `Dispose`) and
produces the core library's equally public `EngineAudio` values - `ISynthesisBackend` is public
specifically so any package, not only this one, can supply its own implementation. It consumes
the sherpa-onnx managed API (`OfflineTts`, `OfflineTtsConfig`; see _SherpaOnnx Design_). It is
constructed only by this system's synthesis
models' `ISynthesisModel.CreateBackend` implementations (see _SpeechSherpa
ModelManagementSubsystem Design_).

### Design

The core library's `DefaultSynthesisBackendFactory` holds no engine-specific knowledge: it
forwards to the selected model's internal `ISynthesisModel.CreateBackend`. Each synthesis model in
this system implements that member by constructing a `SherpaOnnxSynthesisEngine` from its own
sherpa-onnx configuration. The resulting engine is owned by the core library's
`SpeechSynthesizerEngine` and passed to each `SynthesisSession` it creates, which calls `Generate`
from exactly one dedicated-worker call at a time; the engine itself is not thread-safe by
contract. The engine's `SampleRate` is authoritative over the model's best-effort
`PreferredAudioFormat` hint, and the core library resamples to the playback device's format when
they differ.

Constructing the engine loads the model into native memory, so a missing platform native runtime
(`org.k2fsa.sherpa.onnx.runtime.{RID}`, which this package deliberately does not bundle) or
unusable model files surface as an exception from `CreateBackend`. The core library's
`SpeechSynthesizerFactory` catches that exception and degrades it to the honest unavailable
synthesizer.
