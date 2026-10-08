## ModelManagementSubsystem Design

![ModelManagementSubsystem Structure](SherpaModelManagementSubsystemView.svg)

### Overview

The SpeechSherpa ModelManagementSubsystem supplies the concrete, sherpa-onnx-backed speech models
this repository ships, as implementations of the core `Speech` library's
`IRecognitionModel`/`ISynthesisModel` extension point (see _Speech ModelManagementSubsystem
Design_). These units previously lived inside the core library's own ModelManagementSubsystem;
they moved here, unchanged in behavior, so the core library carries no sherpa-onnx or
SharpCompress dependency. Each model declares its own identity, license, download descriptor,
install hook, and audio format, and owns its own sherpa-onnx engine configuration privately,
exposing it to the core library only through the engine-neutral backend it constructs from
`CreateBackend`. It contains the following units:

- **SpeechModelCatalogSherpaExtensions**: the public `AddSherpaModels()` extension method on the
  core library's `SpeechModelCatalog`, registering this subsystem's four models in one call
- **SherpaOnnxZipformerEnRecognitionModel**: a real, concrete `IRecognitionModel`, backing
  sherpa-onnx's `streaming-zipformer-en-2023-06-26` transducer model (Apache-2.0, likely - not
  independently confirmed against an explicit upstream LICENSE file)
- **SherpaOnnxNemotronStreamingEnRecognitionModel**: a second real, concrete
  `IRecognitionModel`, backing NVIDIA's cache-aware FastConformer-RNNT
  `nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25` model, under the NVIDIA Open Model
  License (a custom, non-OSI license, explicitly and visibly distinct from the other model's
  license) - metadata and a download descriptor only; this repository never bundles or
  redistributes NVIDIA's model weights
- **SherpaOnnxVitsLibriTtsEnglishSynthesisModel**: a real, concrete `ISynthesisModel`, backing
  sherpa-onnx's `vits-piper-en_US-libritts_r-medium` VITS/Piper voice (CC BY 4.0 - an
  attribution license, materially different from the other models' Apache-2.0/NVIDIA Open Model
  License terms)
- **SherpaOnnxKokoroEnglishSynthesisModel**: a second real, concrete `ISynthesisModel`, backing
  sherpa-onnx's `kokoro-int8-en-v0_19` Kokoro voice archive (Apache-2.0), declaring 11 genuinely
  distinct voices as a `ChoiceParameter` and owning the real voice-name-to-speaker-id mapping
  `ISynthesisModel.ResolveSpeakerId` needs to make voice selection actually change synthesized
  output
- **TarBz2ArchiveExtractor**: the shared internal `.tar.bz2` extraction helper every model's
  `InstallAsync` calls to unpack its downloaded archive
- **UppercaseTranscriptRestorer**: the shared internal helper that restores casing, a closed
  conservative set of contractions, and terminal punctuation on a recognizer's UPPERCASE,
  unpunctuated raw output, ported from the reference `HiArc.AI.Speech.DictationRestorer`
  implementation; used via `SherpaOnnxZipformerEnRecognitionModel`'s
  `IRecognitionModel.NormalizeText` override

### Interfaces

The subsystem exposes `SpeechModelCatalogSherpaExtensions`,
`SherpaOnnxZipformerEnRecognitionModel`, `SherpaOnnxNemotronStreamingEnRecognitionModel`,
`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`, and `SherpaOnnxKokoroEnglishSynthesisModel` as its
public API (`TarBz2ArchiveExtractor` and `UppercaseTranscriptRestorer` are internal). It consumes
the core library's `SpeechModelCatalog`, `ISpeechModel`, `IRecognitionModel`, `ISynthesisModel`,
`SpeechModelDownloadDescriptor`, `SpeechModelDownloadFile`, and parameter types from the _Speech
ModelManagementSubsystem Design_, and `AudioFormat` from the core AudioSubsystem. Each model's
public `CreateBackend` implementation constructs this system's own `SherpaOnnxRecognitionEngine`
(see _SpeechSherpa RecognitionSubsystem Design_) or `SherpaOnnxSynthesisEngine` (see _SpeechSherpa
SynthesisSubsystem Design_), returned to the core library only as its engine-neutral, equally
public `IRecognitionBackend`/`ISynthesisBackend` seam. `IRecognitionModel`/`ISynthesisModel` and
the `IRecognitionBackend`/`ISynthesisBackend` seams their `CreateBackend` members return are all
fully public contracts - genuine third-party extensibility is a confirmed design goal - so this
assembly needs no `InternalsVisibleTo` grant to implement any of them, and (confirmed by building
and testing with the grant removed) the core library's project file no longer lists this assembly
as an `InternalsVisibleTo` target at all; it behaves as an ordinary external consumer of the core
library's public API, exactly as a genuine third-party model package would.

### Design

`SpeechModelCatalogSherpaExtensions.AddSherpaModels(this SpeechModelCatalog catalog)` is the
single public entry point a host uses to register this system's models, for example
`new SpeechModelCatalog().AddSherpaModels()`. It calls the core library's
`SpeechModelCatalog.AddModels(...)` once with the Zipformer, Nemotron, VITS, and Kokoro models,
in that order, and returns the same catalog instance so registrations chain fluently. It throws
`ArgumentNullException` for a null catalog. The class lives in the `DemaConsulting.Speech.Sherpa`
namespace at the project root, rather than inside this subsystem's folder, because it is the
package's public composition entry point rather than a model; it is traced to this subsystem
because its only purpose is to register this subsystem's models.

Each model implements `CreateBackend` (one-argument form; the parameter-aware recognition overload
keeps the core library's default forwarding) by constructing its engine from its own internal
static `BuildEngineConfig(installedModelDirectory)` result. `BuildEngineConfig` is pure with respect
to library state: it allocates no native resources and never loads the model, so it can be
verified directly without a native runtime, while a missing native runtime or unusable model file
fails at engine construction inside `CreateBackend`, where the core library's composition root
degrades it to an honest unavailable recognizer or synthesizer.

`SherpaOnnxZipformerEnRecognitionModel` and `SherpaOnnxNemotronStreamingEnRecognitionModel` are
Phase 7a's two concrete `IRecognitionModel` implementations, both structurally similar: each
declares its identity/`Id`/`DisplayName`, a `SpeechModelDownloadDescriptor` pointing at the
model's own official upstream GitHub release URL (`k2-fsa/sherpa-onnx`'s `asr-models` tag) with a
real, independently-computed SHA-256 checksum, a mono 16 kHz `AudioFormat`, and an internal static
`BuildEngineConfig`
that builds an `OnlineRecognizerConfig` wiring `OnlineModelConfig.Transducer`'s
`Encoder`/`Decoder`/`Joiner`/`Tokens` paths to the model's int8-quantized files inside its
installed directory, `DecodingMethod = "greedy_search"`, and `EnableEndpoint = 1`. The Zipformer
model additionally sets `ModelConfig.ModelType = "zipformer2"`; the Nemotron model deliberately
leaves `ModelType` unset, since sherpa-onnx's native dispatch logic auto-detects the correct
internal decoding path for a cache-aware FastConformer-RNNT decoder by inspecting the decoder
ONNX file itself - the one structural difference between the two classes. Both models'
`InstallAsync` delegates to `TarBz2ArchiveExtractor` to unpack their downloaded `.tar.bz2`
archive in place before deleting the archive file, mirroring the existing zip-archive
`InstallAsync` precedent established in the test project's fakes. The Nemotron model's XML
documentation and `DisplayName` explicitly and visibly call out its NVIDIA Open Model License -
a custom, non-OSI license materially different from the Zipformer model's Apache-2.0 license -
and its class-level remarks make explicit that the library only stores metadata and a download
descriptor; it never bundles or redistributes NVIDIA's model weights, which a consumer fetches
themselves via `InstallAsync`, on their own explicit action, exactly like every other model.

`TarBz2ArchiveExtractor` is an internal static helper, built on the `SharpCompress` OTS
dependency (see _SharpCompress Design_), that both new recognition models'
`InstallAsync` call to extract a downloaded `.tar.bz2` archive into a target directory,
skipping directory entries and deleting the archive only after every entry extracts
successfully.

`SherpaOnnxVitsLibriTtsEnglishSynthesisModel` is Phase 7b's one concrete `ISynthesisModel`
implementation, completing the catalog with a shippable model for the role Phase 7a left
unaddressed. It declares its identity/`Id`/`DisplayName` (visibly naming CC BY 4.0),
a `SpeechModelDownloadDescriptor` pointing at the model's own official upstream GitHub release
URL (`k2-fsa/sherpa-onnx`'s `tts-models` tag) with a real, independently-computed SHA-256
checksum, and an internal static `BuildEngineConfig` that builds an `OfflineTtsConfig` wiring
`Model.Vits.Model`/`.Tokens`/`.DataDir` to the model's onnx/tokens/espeak-ng-data files inside its
installed directory, this model's own recommended `NoiseScale`/`NoiseScaleW`/`LengthScale`
values (read directly from its published `.onnx.json` config, deliberately different from
another sibling VITS/Piper voice's separately-proven defaults), and no `Lexicon` (this archive
ships none). `PreferredAudioFormat` is declared as mono 22050 Hz - the best-effort rate this
repository's own manual engine load observed for the real model - so a host can request matching
playback before the engine is loaded, while still treating the eventual loaded engine's
`SampleRate` as authoritative. `AudioTagSupport = None` and no `CapabilityProfile` override are
declared, since the default `DefaultModelCapabilityProfile` already provides generically correct
tag-stripping/pause-silence behavior for a model with no native tag support. `InstallAsync`
delegates to the
same `TarBz2ArchiveExtractor` Phase 7a built - reused unchanged, not duplicated - to unpack its
downloaded `.tar.bz2` archive in place before deleting the archive file. The model's own
`MODEL_CARD` declares 904 distinct speakers; as of Phase 7b, neither `ISynthesisModel` nor
`SherpaOnnxSpeechSynthesizer` exposed speaker selection (`GenerateSegment` hard-coded
`speakerId: 0` for every synthesis model), so this class exposed only that one default speaker, a
documented, accepted, out-of-scope limitation rather than a silently-dropped capability. Phase 10
closed the underlying `speakerId: 0` hard-coding gap for the whole subsystem by adding
`ISynthesisModel.ResolveSpeakerId`, which the new Kokoro model below overrides immediately; this
VITS model's own 904-speaker limitation was closed in a later pass, which declares a plain
numeric `speaker` `NumericParameter` (0-903 - LibriTTS-R's speaker embeddings have no published
human-readable name mapping, unlike Kokoro's small, named voice set) and overrides
`ResolveSpeakerId` to read it, resolving gracefully to the default speaker `0` for a missing,
non-numeric, or out-of-range selection.

`SherpaOnnxKokoroEnglishSynthesisModel` is Phase 10's second concrete `ISynthesisModel`
implementation, and the first model in the subsystem to genuinely exercise real,
model-owned speaker selection. It declares its identity/`Id`/`DisplayName` (visibly naming
Apache-2.0), a `SpeechModelDownloadDescriptor` pointing at the model's own official upstream
GitHub release URL (`k2-fsa/sherpa-onnx`'s `tts-models` tag) with a real,
independently-computed SHA-256 checksum, a `Parameters` list containing one `voice`
`ChoiceParameter` with 11 confirmed options, and an internal static `BuildEngineConfig` that builds an
`OfflineTtsConfig` wiring `Model.Kokoro.Model`/`.Voices`/`.Tokens`/`.DataDir` to the model's
onnx/voices/tokens/espeak-ng-data files inside its installed directory, `LengthScale = 1.0f`,
and no `DictDir`/`Lexicon`/`Lang` (this English-only archive ships none of those).
`PreferredAudioFormat` is declared as mono 24000 Hz - the best-effort rate this repository's own
manual engine load observed for the real model - so a host can request matching playback before
the engine is loaded, while still treating the eventual loaded engine's `SampleRate` as
authoritative. `AudioTagSupport = None` and no `CapabilityProfile` override are declared, for the
same reason as the VITS model.
`InstallAsync` delegates to the same `TarBz2ArchiveExtractor` reused unchanged across every
archive-based model in this subsystem. Its `ISynthesisModel.ResolveSpeakerId` override is this
model's own owned knowledge: a confirmed `id2speaker` ordering mapping each of its 11 declared
voice names to sherpa-onnx's real integer speaker id, falling back to the default voice's id for
a null bag, a missing key, or an unrecognized value - never throwing. See
`sherpa-onnx-kokoro-en-synthesis-model.md` for the full confirmed voice ordering and its
provenance.
