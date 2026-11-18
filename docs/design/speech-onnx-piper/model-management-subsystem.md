## ModelManagementSubsystem Design

![ModelManagementSubsystem Structure](PiperModelManagementSubsystemView.svg)

### Overview

The SpeechOnnxPiper ModelManagementSubsystem supplies this package's one concrete synthesis model as an implementation
of the Speech library's `ISynthesisModel` extension point (see *Speech ModelManagementSubsystem Design*), together with
the reader of the voice configuration that the model and the synthesis subsystem both need. It contains the following
units:

- **SpeechModelCatalogPiperExtensions**: the public `AddPiperModels()` extension method on the Speech library's
  `SpeechModelCatalog`, registering this subsystem's one model in one call
- **OnnxPiperLibriTtsEnSynthesisModel**: the concrete `ISynthesisModel` for the Piper voice `en_US-libritts_r-medium`
  (CC BY 4.0, 904 speakers, 22,050 Hz), with the internal `PiperArchiveExtractor` that installs the downloaded archive
  and the internal `PiperCapabilityProfile` that joins the clause pieces of a sentence into one segment
- **PiperVoiceConfig**: the internal reader of the voice's `.onnx.json` file (symbol table, sample rate, speaker count,
  inference scales)

### Interfaces

The subsystem exposes `SpeechModelCatalogPiperExtensions` and `OnnxPiperLibriTtsEnSynthesisModel` as its public API
(`PiperVoiceConfig`, `PiperArchiveExtractor` and `PiperCapabilityProfile` are internal). It consumes the Speech
library's `SpeechModelCatalog`, `ISynthesisModel`, `IModelCapabilityProfile` with `DefaultModelCapabilityProfile` and
`SentenceChunker`, `SpeechModelDownloadDescriptor`, `SpeechModelDownloadFile` and parameter types, `AudioFormat` from
Speech's AudioSubsystem, `EnglishTextAnalyzer` of SpeechTextEn and the sibling SpeechOnnx system's
`OnnxExecutionProviderSelector`. The model's `CreateBackend` constructs this system's `PiperPhonemePipeline` and
`OnnxPiperSynthesisEngine` (see *SpeechOnnxPiper SynthesisSubsystem Design*) and returns the engine to Speech only as
its public `ISynthesisBackend` seam.

### Design

`SpeechModelCatalogPiperExtensions.AddPiperModels(this SpeechModelCatalog catalog, IReadOnlyList<string>?
preferredExecutionProviderNames = null)` is the subsystem's single public entry point, for example
`new SpeechModelCatalog().AddPiperModels()`. It calls `SpeechModelCatalog.AddModels(...)` once with a new
`OnnxPiperLibriTtsEnSynthesisModel(preferredExecutionProviderNames)` and returns the same catalog so that registrations
chain. It throws `ArgumentNullException` for a null catalog. The class lives in the `DemaConsulting.Speech.Onnx.Piper`
namespace at the project root rather than in this subsystem's folder, because it is the package's composition entry
point; it is traced to this subsystem because its only purpose is to register this subsystem's one model. Following the
convention of `SpeechModelCatalogSherpaExtensions` and `SpeechModelCatalogKokoroExtensions`, this extension class has no
separate unit-level design, requirements or verification file: it is documented here as a subsystem unit, and its
requirements are consolidated in the subsystem requirements file.

`OnnxPiperLibriTtsEnSynthesisModel` declares the identifier `piper-onnx-libritts-r-en-us-medium`, the Synthesis role,
the license CC BY 4.0 with its canonical link, the audio-tag support `None` and the shared `EnglishTextAnalyzer` as its
text analyzer; owns the download descriptor of the pinned archive and its installation; resolves the one `speaker`
parameter; and creates the synthesis backend. Its design, including the Voice Download decision (D4), is in
*OnnxPiperLibriTtsEnSynthesisModel Design*. `PiperVoiceConfig` is documented in *PiperVoiceConfig Design*.

**Order of work in `CreateBackend`.** The configuration is read and checked first, the phoneme pipeline is built from
it, and only then is the ONNX Runtime session created (through `OnnxExecutionProviderSelector`, with the engine's probe
inference as the validation of each accelerated candidate). The engine constructor, the only step after the session
exists, cannot fail, so a configuration that is invalid fails without a native session ever existing.

**Error handling.** `InstallAsync` and `CreateBackend` report an archive or a voice that cannot be used with
`InvalidDataException`, with a message that says what is missing or wrong; an empty path argument is an
`ArgumentException`. The downloader discards the whole staging directory when an installation fails, which is why a
failed installation leaves the archive in place.
