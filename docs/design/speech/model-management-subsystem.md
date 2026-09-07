## ModelManagementSubsystem Design

![ModelManagementSubsystem Structure](ModelManagementSubsystemView.svg)

### Overview

The ModelManagementSubsystem provides the storage/download machinery and the model catalog/
contract machinery for speech models. Sub-phase 2a implements a per-user on-disk store with
atomic install/replace semantics, and a queued downloader that fetches, SHA-256-verifies, and
atomically installs a model's declared files. Sub-phase 2b adds the generic model catalog/
contract seam on top: a typed tunable-parameter descriptor set, the common `ISpeechModel`
contract with its `IRecognitionModel`/`ISynthesisModel` role markers, and `SpeechModelCatalog`
itself. Phase 7a populated that previously-empty catalog with the library's first two real,
concrete `IRecognitionModel` classes - resolving the "compiled-in registry is always empty"
defect a user discovered in the demo app - plus a shared `.tar.bz2` archive-extraction helper
both new models' `InstallAsync` use. Phase 7b added the library's first real, concrete
`ISynthesisModel` class, reusing (not duplicating) that same archive-extraction helper, so every
model role the library defines had at least one shippable model - but that model exposed only
its default speaker, a documented out-of-scope limitation. Phase 10 adds the library's second
real, concrete `ISynthesisModel` class, Kokoro (11 genuinely distinct voices), and closes that
limitation for the whole subsystem by adding a `ResolveSpeakerId` hook to `ISynthesisModel`. A
later pass then closes the Phase 7b VITS model's own remaining 904-speaker limitation too, by
declaring a plain numeric `speaker` parameter (0-903, since LibriTTS-R's speaker embeddings have
no published name mapping) and overriding `ResolveSpeakerId` for it, so voice/speaker selection
now genuinely works end-to-end for every synthesis model this subsystem registers. Phase 12 adds
a recognition-only `IRecognitionModel.NormalizeText(text, isFinal)` hook (a default pass-through,
distinct from the shared `ISpeechModel.NormalizeText(string)` used on the synthesis side) and the
shared `UppercaseTranscriptRestorer` helper, so a model whose raw output empirically "yells"
(UPPERCASE, unpunctuated - confirmed for the Zipformer model via a real audio spike) can restore
readable, cased, punctuated prose before `RecognitionSubsystem` surfaces it to consumers; the
Nemotron model was empirically confirmed to already produce proper casing and therefore keeps
the pass-through default. A later pass adds a two-argument, parameter-value-aware default
hook to `IRecognitionModel.CreateEngineConfig`, bringing the recognition side to parity with the
synthesis side's `ResolveSpeakerId` seam, with zero changes required to either shipped recognition
model since neither declares a parameter yet. It contains the following units:

- **SpeechModelStore** / **SpeechModelStoreOptions** / **SpeechModelStoreException**: the
  per-user on-disk layout, atomic `current/` swap, install-state query, uninstall, and
  best-effort cleanup, plus the storage-root override options and the exception thrown only by
  an explicit uninstall that cannot complete
- **SpeechModelDownloader**: the queued (one-at-a-time) fetch → SHA-256 verify → atomic-install
  orchestrator, defining honest `SpeechModelDownloadOutcome` result states
- **SpeechModelDownloadFile** / **SpeechModelDownloadDescriptor**: HTTPS-only URL(s) plus
  SHA-256 checksum descriptor types describing what a downloader fetches and verifies
- **SpeechModelDownloadProgress**: the `IProgress<T>`-compatible download-progress payload
- **IModelDownloadClient** / **HttpModelDownloadClient**: the mockable HTTP-fetch seam and its
  real `HttpClient`-backed implementation
- **SpeechModelDescriptorEnums** (`SpeechModelRole`, `SpeechModelState`,
  `SpeechModelAudioTagSupport`): the fixed, small declaration-shape enums a model and the catalog
  are built around
- **SpeechModelParameters** (`ISpeechModelParameter`, `NumericParameter`, `ChoiceParameter`,
  `BooleanParameter`): the typed, self-describing tunable-parameter descriptor hierarchy
- **SpeechModelContract** (`ISpeechModel`, `IRecognitionModel`, `ISynthesisModel`): the common
  per-model contract plus its two role-specific interfaces - `IRecognitionModel` exposing a
  public `AudioFormat` and internal engine-construction members, and `ISynthesisModel` exposing
  a public best-effort `PreferredAudioFormat` plus its internal synthesis hooks
- **SpeechModelDescriptor**: the immutable catalog read-model pairing one `ISpeechModel` with its
  current `SpeechModelState`
- **SpeechModelCatalog**: enumerates the compiled-in known-model registry alongside each model's
  install state, and orchestrates downloading a known model by id through `SpeechModelDownloader`
- **SherpaOnnxZipformerEnRecognitionModel**: the first real, concrete `IRecognitionModel`,
  backing sherpa-onnx's `streaming-zipformer-en-2023-06-26` transducer model (Apache-2.0,
  likely - not independently confirmed against an explicit upstream LICENSE file)
- **SherpaOnnxNemotronStreamingEnRecognitionModel**: the second real, concrete
  `IRecognitionModel`, backing NVIDIA's cache-aware FastConformer-RNNT
  `nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25` model, under the NVIDIA Open Model
  License (a custom, non-OSI license, explicitly and visibly distinct from the other model's
  license) - metadata and a download descriptor only; the library never bundles or redistributes
  NVIDIA's model weights
- **TarBz2ArchiveExtractor**: the shared internal `.tar.bz2` extraction helper both new
  recognition models' `InstallAsync` call to unpack their downloaded archive
- **UppercaseTranscriptRestorer**: the shared internal helper that restores casing, a closed
  conservative set of contractions, and terminal punctuation on a recognizer's UPPERCASE,
  unpunctuated raw output, ported from the reference `HiArc.AI.Speech.DictationRestorer`
  implementation; used via `SherpaOnnxZipformerEnRecognitionModel`'s
  `IRecognitionModel.NormalizeText` override
- **SherpaOnnxVitsLibriTtsEnglishSynthesisModel**: the first real, concrete `ISynthesisModel`,
  backing sherpa-onnx's `vits-piper-en_US-libritts_r-medium` VITS/Piper voice (CC BY 4.0 -
  an attribution license, materially different from the other two models' Apache-2.0/NVIDIA Open
  Model License terms), completing the catalog with a shippable model of every role
- **SherpaOnnxKokoroEnglishSynthesisModel**: the second real, concrete `ISynthesisModel`,
  backing sherpa-onnx's `kokoro-int8-en-v0_19` Kokoro voice archive (Apache-2.0), declaring 11
  genuinely distinct voices as a `ChoiceParameter` and owning the real voice-name-to-speaker-id
  mapping `ISynthesisModel.ResolveSpeakerId` needs to make voice selection actually change
  synthesized output

### Interfaces

The subsystem exposes `SpeechModelStore`, `SpeechModelStoreOptions`, `SpeechModelStoreException`,
`SpeechModelDownloader`, `SpeechModelDownloadOutcome`, `SpeechModelDownloadResult`,
`SpeechModelDownloadFile`, `SpeechModelDownloadDescriptor`, `SpeechModelDownloadProgress`,
`IModelDownloadClient`, `HttpModelDownloadClient`, `SpeechModelRole`, `SpeechModelState`,
`SpeechModelAudioTagSupport`, `ISpeechModelParameter`, `NumericParameter`, `ChoiceParameter`,
`ChoiceParameterOption`, `BooleanParameter`, `ISpeechModel`, `IRecognitionModel`,
`ISynthesisModel`, `SpeechModelDescriptor`, `SpeechModelCatalog`,
`SherpaOnnxZipformerEnRecognitionModel`, `SherpaOnnxNemotronStreamingEnRecognitionModel`,
`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`, and `SherpaOnnxKokoroEnglishSynthesisModel` as its
public API (`TarBz2ArchiveExtractor` is internal). It consumes `ISpeechDiagnostics` from the
Diagnostics subsystem to report structural download-failure facts without ever exposing raw
model bytes. It also consumes `AudioFormat` from the AudioSubsystem as a narrow plain-data
dependency for public model format declarations.

### Design

`SpeechModelStore` owns the on-disk layout resolving the download-while-in-use concern
(download-while-in-use). For each model id, it manages `{root}/{model-id}/current/` (the
stable, installed content, only ever replaced by a directory rename),
`{root}/{model-id}/install-manifest.json` (a sidecar written only after a successful swap so
`IsInstalled` can report cheaply without re-hashing gigabytes on every query), and
`{root}/{model-id}/.tmp/{operation-id}/` (scratch space for an in-progress
download/install, never read as installed content). `current/` is never touched while a
download is being fetched and verified, so an in-use recognizer/synthesizer continues reading
unaffected content until the swap completes.

`SpeechModelDownloader` is the only caller of `SpeechModelStore`'s internal staging/swap
members. It serializes downloads one at a time via an internal single-flight lock, fetches each
declared file through the injected `IModelDownloadClient`, verifies its SHA-256 checksum, and -
when its `ISpeechModel`-aware `DownloadAsync` overload is used - invokes the model's own
`InstallAsync` hook to unpack the staged files in place (a no-op by default), before only then
handing the fully verified (and, if applicable, unpacked) staging directory to `SpeechModelStore`
for the atomic swap. A checksum mismatch, transport failure, install-hook failure, or
cancellation discards the staging directory and leaves any prior successful install of the same
model completely untouched - `SpeechModelDownloadOutcome` never reports `Installed` for a
corrupted, partial, checksum-mismatched, or install-hook-failed download.

`IModelDownloadClient` mirrors the existing `IPortAudioApi`/`IPortAudioStream` seam pattern: a
small library-owned interface with one real implementation (`HttpModelDownloadClient`, backed by
`System.Net.Http.HttpClient`) and hand-written fakes used in tests for fast, deterministic
coverage of `SpeechModelDownloader`'s orchestration logic without any real network access.
`HttpModelDownloadClient` itself is additionally verified against a genuine loopback
`System.Net.HttpListener` server to prove it truly performs an HTTP download with progress
reporting.

`ISpeechModel` is the common contract every model's backing class implements (through either
`IRecognitionModel` or `ISynthesisModel`, never directly): identity (`Id`/`DisplayName`), `Role`,
its declared `Parameters` (`ISpeechModelParameter` instances - `NumericParameter`,
`ChoiceParameter`, or `BooleanParameter`, each self-validating an internally consistent range/
option-set/default at construction), its declared `AudioTagSupport` (a declaration only - the
Layer 2 rendering logic is Phase 4), its `DownloadDescriptor`, its `InstallAsync` hook (a no-op
default, overridable to unpack an archive payload), and its `NormalizeText` hook (an identity
default, overridable for Phase 4 text normalization). `IRecognitionModel` now exposes a public
plain-data `AudioFormat` declaration, while keeping `CreateEngineConfig(installedModelDirectory)`
internal because it returns a sherpa-onnx type; this lets hosts compose capture devices around a
model's required format without leaking native engine configuration into the public API.
`ISynthesisModel` similarly exposes a public best-effort `PreferredAudioFormat` hint, while
keeping `CreateEngineConfig`, `CapabilityProfile`, and `ResolveSpeakerId(parameterValues)`
internal. The hint is intentionally non-authoritative: the real synthesis output rate is still
the loaded engine's `ISynthesisEngine.SampleRate`.

`SpeechModelCatalog` composes a compiled-in `KnownModels` list - as of Phase 7a, this phase's two
real recognition models - with a `SpeechModelStore` and a `SpeechModelDownloader`. `Enumerate()`
builds one immutable `SpeechModelDescriptor` snapshot per known model, resolving each model's
`SpeechModelState` by combining `SpeechModelStore.IsInstalled` (installed/not-installed) with
in-memory tracking of which model ids currently have a `DownloadAsync` call in flight
(`Downloading`) and which model ids' most recent attempt did not result in an installed model
(`FailedOrCorrupt`) - tracking scoped to one catalog instance's lifetime, since a bare on-disk
store has no durable concept of "currently downloading" or "last attempt failed". `DownloadAsync(
modelId, ...)` looks up the named model and delegates to `SpeechModelDownloader.DownloadAsync(
model, ...)` - the `ISpeechModel`-aware overload, so the model's own `InstallAsync` hook always
runs - throwing only when `modelId` matches no known model - an explicit, user-invoked action,
never composition or enumeration.

`SherpaOnnxZipformerEnRecognitionModel` and `SherpaOnnxNemotronStreamingEnRecognitionModel` are
Phase 7a's two concrete `IRecognitionModel` implementations, both structurally similar: each
declares its identity/`Id`/`DisplayName`, a `SpeechModelDownloadDescriptor` pointing at the
model's own official upstream GitHub release URL (`k2-fsa/sherpa-onnx`'s `asr-models` tag) with a
real, independently-computed SHA-256 checksum, a mono 16 kHz `AudioFormat`, and a
`CreateEngineConfig`
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

`TarBz2ArchiveExtractor` is an internal static helper, built on the new `SharpCompress` OTS
dependency (see `docs/design/ots/sharp-compress.md`), that both new recognition models'
`InstallAsync` call to extract a downloaded `.tar.bz2` archive into a target directory,
skipping directory entries and deleting the archive only after every entry extracts
successfully.

`SherpaOnnxVitsLibriTtsEnglishSynthesisModel` is Phase 7b's one concrete `ISynthesisModel`
implementation, completing the catalog with a shippable model for the role Phase 7a left
unaddressed. It declares its identity/`Id`/`DisplayName` (visibly naming CC BY 4.0),
a `SpeechModelDownloadDescriptor` pointing at the model's own official upstream GitHub release
URL (`k2-fsa/sherpa-onnx`'s `tts-models` tag) with a real, independently-computed SHA-256
checksum, and a `CreateEngineConfig` that builds an `OfflineTtsConfig` wiring
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
`ChoiceParameter` with 11 confirmed options, and a `CreateEngineConfig` that builds an
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
