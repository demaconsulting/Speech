<!-- cspell:ignore ALSA portaudio -->
# Introduction

This document provides the detailed design for the Speech library, a cross-platform .NET
library providing local, offline speech-to-text (STT) and text-to-speech (TTS) services for
desktop applications.

## Purpose

The purpose of this document is to serve as the design entry point and provide detailed design
specifications for the Speech system. This documentation enables formal code review by
providing implementation specifications, supports compliance auditing by maintaining clear
traceability from requirements through design to code, aids maintenance by documenting system
structure and interactions, and ensures quality assurance through detailed technical
specifications.

This document is intended for:

- Software developers implementing and maintaining the system
- Code reviewers validating implementation against design
- Compliance auditors tracing requirements through design to implementation
- Quality assurance teams validating system behavior

## Scope

This document covers the detailed design of the Speech system and its constituent software
items, of the SpeechDemo application system and its constituent software items, of the
SpeechCli command-line tool system and its constituent software items, of the SpeechSherpa
model-provider library system and its constituent software items, and of the four ONNX
Runtime-based sibling library systems (SpeechOnnx, SpeechOnnxKokoro, SpeechOnnxNemotronStt, and
SpeechOnnxChatterboxTurbo) and their constituent software items, specifically:

- **Speech (System)** — The complete .NET library system providing speech capture, recognition,
  and synthesis capabilities to host applications
- **Diagnostics (Subsystem)** — Reports structural diagnostic events to an optional
  host-supplied sink
- **AudioSubsystem (Subsystem)** — Audio capture/playback contracts, name-based selection,
  honest unavailable fallbacks, real PortAudio-backed device/probe implementations, file-backed
  WAV capture and playback devices, and the managed audio-file pipeline: the in-memory
  `AudioClip`, WAV and FLAC decoding behind `AudioFileReader`, and the windowed-sinc
  `AudioResampler`
- **PortAudio (Subsystem)** — Internal child subsystem under AudioSubsystem that isolates
  PortAudioSharp2 and supplementary host-API P/Invoke bindings behind mockable abstractions
- **ModelManagementSubsystem (Subsystem)** — Per-user speech model storage with atomic
  install/repair/uninstall, the queued download-verify-install orchestration and mockable
  HTTP seam that fetch and install a model's files with honest failure states, and the model
  catalog/contract seam: typed tunable-parameter descriptors, the common per-model contract
  and its role-marker interfaces (the extension point concrete models implement), and catalog
  enumeration of the host-registered known-model list alongside install state; the optional
  download-mirror redirect (`DownloadMirror`); and the reference-voice pack, its installer and
  the `ReferenceVoiceLibrary` that lists user-folder and installed-pack voices
- **RecognitionSubsystem (Subsystem)** — Streaming speech-to-text: the public async
  Engine/Session recognizer contract and result types, the composition root (`LoadAsync`/
  `CreateSessionAsync`) with honest unavailable fallbacks and engine exclusivity lease, the
  capture-to-engine pipeline with its audio-format converter, and the public, mockable
  recognition-backend seam with its model-driven default backend factory
- **SynthesisSubsystem (Subsystem)** — Text-to-speech: the closed, fixed Natural Language Audio
  Tag vocabulary grouped into kinds and the model-independent Layer 1 parser that recognizes
  bracket syntax against it, the Layer 2 rendering strategy that turns a parsed span sequence
  into a model-appropriate `SpeechPlan`, sentence chunking for sequential chunked synthesis, the public
  async Engine/Session synthesis/playback contract with its composition root and honest
  unavailable fallbacks, the public, mockable synthesis-backend seam with its model-driven
  default backend factory, per-segment voice selection, and the playback-format converter

The following software items of the SpeechDemo system are also covered:

- **SpeechDemo (System)** — The Avalonia desktop application that demonstrates the Speech
  library from the position of an ordinary consumer, using only its public API. It is a sibling
  system to Speech, not a subsystem of it: it is separately built and separately run, has its
  own users and its own externally visible behavior, and the library must never depend on it
- **App, AppLaunchOptions, MirrorOptionsFactory, Program (Units)** — The units that belong
  directly to the SpeechDemo system: the composition root application class, the command-line
  options, the shared mirror-option validation, and the process entry point
- **ShellSubsystem (Subsystem)** — The application's manual composition root and window shell,
  presenting one titled, selectable panel per demonstrated capability; units
  `MainWindowViewModel` and `DemoPanelViewModel`
- **DeviceSelectionSubsystem (Subsystem)** — The capture and playback device pickers: a
  demo-owned enumeration seam over the library's device probes plus the presentation state that
  lists devices, tracks the chosen device by name, and explains an empty machine honestly; units
  `AudioDeviceService` (with its `IAudioDeviceService` seam) and `DeviceSelectionViewModel`
- **ModelCatalogSubsystem (Subsystem)** — The model catalog and download panel: a demo-owned
  catalog seam over the library's model catalog and reference-voice library (paired by
  `ModelCatalogComposition`) plus the row presentation for models and reference-voice packs,
  download progress and honest failure reporting, and the explanatory empty-catalog message;
  units `ModelCatalogService` (with its `IModelCatalogService` seam), `ModelCatalogViewModel`,
  `ModelListItemViewModel`, and `ReferenceVoicePackListItemViewModel`
- **ModelSettingsSubsystem (Subsystem)** — The generic per-model tunable-parameter settings
  panel: presenters for the library's numeric, choice, and boolean parameter kinds, chosen by
  concrete type alone, plus the honest empty state shown with no model selected or a model
  declaring no parameters; units `ModelSettingsViewModel` and `ParameterViewModels` (the base,
  numeric, choice, and boolean presenters)
- **RecognitionPanelSubsystem (Subsystem)** — The speech-to-text panel: a demo-owned session
  seam over the library's `SpeechRecognizerFactory` narrowing a public model to the library's
  recognition role, and the Start/Stop streaming lifecycle with progressive partial-then-final
  transcript rendering and honest reporting of every unavailable state, plus the opt-in
  `CaptureDebugRecorder`/`WavFileWriter` diagnostic that records the captured audio to a WAV file;
  units `RecognizerSessionFactory` (with its `IRecognizerSessionFactory` seam) and
  `RecognitionPanelViewModel`
- **SynthesisPanelSubsystem (Subsystem)** — The text-to-speech panel: a demo-owned session seam
  over the library's `SpeechSynthesizerFactory` narrowing a public model to the library's
  synthesis role, the embedded `ModelSettingsSubsystem` panel for the selected model's
  parameters, example Natural Language Audio Tag hints, and the Play/Stop lifecycle with honest
  reporting of every unavailable state; units `SynthesizerSessionFactory` (with its
  `ISynthesizerSessionFactory` seam) and `SynthesisPanelViewModel`

The following software items of the SpeechCli system are also covered:

- **SpeechCli (System)** — A cross-platform .NET global tool, packaged as
  `DemaConsulting.Speech.Cli` and installed under the command name `speech-cli`, that exposes
  the library's model management, audio device inspection, text-to-speech, and speech-to-text
  capabilities from the command line. It is a sibling system to Speech, not a subsystem of it: it
  is separately built, separately packaged, and separately versioned, has its own users
  (command-line operators and scripts), and the library must never depend on it
- **ModelCommandsSubsystem (Subsystem)** — The model-management and reference-voice subcommands (`list-models`,
  `model-info`, `download`, `uninstall`, `clean`, `list-voices`, `voices`) and the CLI-owned catalog seam
  (`ICliModelCatalog`/`SpeechModelCatalogAdapter`/`CliModelCatalogFactory`) they share over the
  library's `SpeechModelCatalog`/`SpeechModelStore`
- **DeviceCommandsSubsystem (Subsystem)** — The three device-related subcommands
  (`list-devices`, `devices test`, `doctor`), consuming the library's already-public
  `IAudioCaptureDeviceProbe`/`IAudioPlaybackDeviceProbe`/`AudioDeviceFactory` directly, with no
  CLI-owned seam wrapper
- **SynthesisCommandSubsystem (Subsystem)** — The one text-to-speech subcommand (`speak`),
  extending `ModelCommandsSubsystem`'s `ICliModelCatalog` seam with synthesis-side members
  rather than introducing a second, competing seam
- **RecognitionCommandSubsystem (Subsystem)** — The one speech-to-text subcommand
  (`recognize`) plus the `SilenceTimeoutRecognizerSession` idle-timeout utility, extending
  `ModelCommandsSubsystem`'s `ICliModelCatalog` seam with recognition-side members symmetric to
  the synthesis pair
- **ConversationCommandSubsystem (Subsystem)** — The one voice-conversation subcommand (`ask`),
  running a speak-then-listen turn as one invocation by reusing `SynthesisCommandSubsystem`'s
  and `RecognitionCommandSubsystem`'s existing `ICliModelCatalog` seam members (no new catalog
  member) and adding one CLI-owned capture-device seam (`ICliCaptureDeviceSource`)

The following software items of the SpeechSherpa system are also covered:

- **SpeechSherpa (System)** — A .NET library, packaged as `DemaConsulting.Speech.Sherpa`, that
  supplies concrete sherpa-onnx-backed speech models and inference backends for the Speech
  library. It is a sibling system to Speech, not a subsystem of it: it is separately built and
  separately packaged, with its own dependencies. Unlike the other sibling systems it both
  consumes Speech and extends it, by implementing Speech's `IRecognitionModel`/`ISynthesisModel`
  extension seam; `Speech` must never depend on it
- **ModelManagementSubsystem (Subsystem)** — The `AddSherpaModels()` catalog extension method,
  the two recognition models and two synthesis models implementing Speech's model contract, and
  the shared `.tar.bz2` archive-extraction and uppercase-transcript-restoration helpers they use
- **RecognitionSubsystem (Subsystem)** — The real sherpa-onnx streaming recognition engine
  implementing Speech's public recognition-backend seam, including its per-model post-endpoint
  warm-up replay mitigation
- **SynthesisSubsystem (Subsystem)** — The real sherpa-onnx offline text-to-speech engine
  implementing Speech's public synthesis-backend seam

The following software items of the four ONNX Runtime-based sibling systems are also covered.
Each is a sibling system to Speech, not a subsystem of it, and the library must never depend on
any of them:

- **SpeechOnnx (System)** — The `DemaConsulting.Speech.Onnx` library of shared ONNX Runtime
  infrastructure reusable by any ONNX Runtime-backed model package; it declares no models of its
  own and does not depend on the Speech library's types
- **OnnxRuntimeSubsystem (Subsystem of SpeechOnnx)** — `OnnxExecutionProviderSelector`, which
  builds an `InferenceSession` by trying a caller-supplied ordered list of accelerated execution
  providers (each optionally validated by a probe) before falling back to the CPU provider
- **SpeechOnnxKokoro (System)** — The `DemaConsulting.Speech.Onnx.Kokoro` library, a concrete
  ONNX Runtime-backed Kokoro-82M text-to-speech model and engine built on SpeechOnnx and
  implementing Speech's `ISynthesisModel`/`ISynthesisBackend` seam, with its own embedded
  lexicon phonemizer and phoneme vocabulary
- **ModelManagementSubsystem (Subsystem of SpeechOnnxKokoro)** — The `AddKokoroModels()` catalog
  extension, the Kokoro model class, and the text normalizer, lexicon phonemizer and phoneme
  vocabulary units
- **SynthesisSubsystem (Subsystem of SpeechOnnxKokoro)** — `OnnxKokoroSynthesisEngine`, the real
  synthesis backend
- **SpeechOnnxNemotronStt (System)** — The `DemaConsulting.Speech.Onnx.NemotronStt` library, a
  concrete ONNX Runtime-backed streaming speech-recognition model and engine for NVIDIA Nemotron
  built on SpeechOnnx and implementing Speech's `IRecognitionModel`/`IRecognitionBackend` seam
- **ModelManagementSubsystem (Subsystem of SpeechOnnxNemotronStt)** — The `AddNemotronSttModels()`
  catalog extension, the Nemotron recognition model class, and the vocabulary loader
- **RecognitionSubsystem (Subsystem of SpeechOnnxNemotronStt)** — `OnnxNemotronRecognitionEngine`
  with its streaming feature extractor, RNN-T greedy decoder, and silence-run limiter
- **SpeechOnnxChatterboxTurbo (System)** — The `DemaConsulting.Speech.Onnx.ChatterboxTurbo`
  library, a concrete ONNX Runtime-backed voice-cloning text-to-speech model and engine
  implementing Speech's `ISynthesisModel`/`ISynthesisBackend` seam, with its own embedded
  tokenizer, generation loop, and audio-tag rendering policy
- **ModelManagementSubsystem (Subsystem of SpeechOnnxChatterboxTurbo)** — The
  `AddChatterboxTurboModels()` catalog extension, the synthesis model, the byte-level BPE
  tokenizer, and the reference-voice type
- **SynthesisSubsystem (Subsystem of SpeechOnnxChatterboxTurbo)** — The synthesis engine, ONNX
  networks, token selector, reference-clip conditioning, and capability profile

The following OTS items are also covered:

- **PortAudioSharp2** — managed PortAudio binding and transitive native runtime carrier
- **System.Numerics.Tensors** — vectorized `TensorPrimitives` used by the Speech audio pipeline
- **Microsoft.ML.OnnxRuntime** — managed ONNX Runtime API used by the SpeechOnnx family of
  libraries
- **SherpaOnnx** — managed local speech-inference API used by SpeechSherpa
- **SharpCompress** — managed `.tar.bz2` archive reader used by SpeechSherpa model installs
- **Avalonia** — cross-platform desktop UI framework hosting the SpeechDemo application
- **CommunityToolkit.Mvvm** — MVVM change-notification and command source generators
- **BuildMark** — build-notes documentation tool
- **FileAssert** — document assertion tool
- **Pandoc** — Markdown-to-HTML conversion tool
- **ReqStream** — requirements traceability tool
- **ReviewMark** — file review enforcement tool
- **SarifMark** — SARIF report conversion tool
- **SonarMark** — SonarCloud quality report tool
- **SysML2Tools** — architecture model lint and diagram rendering tool
- **VersionMark** — tool-version documentation tool
- **WeasyPrint** — HTML-to-PDF conversion tool
- **xUnit** — unit-testing framework

Version applicability: This design applies to all versions of the Speech.

The following topics are explicitly excluded from this design documentation:

- External library internals beyond the integration points used by this repository
- Build pipeline configuration and CI/CD processes
- Deployment, packaging, and distribution mechanisms
- Infrastructure and hosting environment details
- Test projects and test infrastructure

## Software Structure

The software structure is modeled in SysML2 under `docs/sysml2/` and rendered to the
diagram below by SysML2Tools as part of the build pipeline. AI agents should query the
SysML2 model directly rather than parsing this diagram or the prose below.

![Software Structure](SoftwareStructureView.svg)

This system is structured with five subsystems directly under the system level: `Diagnostics`,
`AudioSubsystem`, `ModelManagementSubsystem`, `RecognitionSubsystem`, and
`SynthesisSubsystem`, described in turn below. `AudioSubsystem` contains the public audio-device
contracts, the real PortAudio-backed implementations, the file-backed WAV capture and playback
devices, and the managed audio-file pipeline (WAV and FLAC decoding behind `AudioFileReader`,
`AudioClip`, and the windowed-sinc `AudioResampler`, which uses System.Numerics.Tensors). It also
contains a child `PortAudio` subsystem that
isolates the PortAudioSharp2 wrapper and the supplementary host-API P/Invoke bindings behind
internal, mockable abstractions. `ModelManagementSubsystem` provides
per-user speech model storage with atomic install/repair/uninstall, a queued
download-verify-install orchestration with a mockable HTTP seam and an optional download mirror,
the reference-voice library and installable reference-voice packs used by voice-cloning models,
and the model catalog/
contract seam (typed tunable parameters, the common model contract, and catalog enumeration)
that the RecognitionSubsystem and SynthesisSubsystem consume via the `IRecognitionModel`/
`ISynthesisModel` contracts. Those contracts are the library's model extension point: the
library ships zero built-in models, and a host registers concrete models with the catalog
through `SpeechModelCatalog.AddModels(...)`. `RecognitionSubsystem`
consumes both of the above: it defines the public async Engine/Session
streaming speech-to-text contract (`ISpeechRecognizerEngine`/`IRecognitionSession`), composing a
recognizer engine for an installed recognition model via `LoadAsync` and then a session bound to
a capture device via the engine's `CreateSessionAsync` without ever throwing for an ordinary
machine state, and converts captured audio into the format a model requires before streaming it
through a public, mockable recognition-backend seam (`IRecognitionBackend`; only its factory is
internal). `SynthesisSubsystem` provides the text-to-speech side: its closed, fixed Natural Language Audio
Tag vocabulary and the model-independent Layer 1 parser (`AudioTagCatalog`/`AudioTagParser`) that
recognizes bracket syntax against it, its Layer 2 rendering strategy
(`IModelCapabilityProfile`/`DefaultModelCapabilityProfile`) that turns a parsed span sequence
into a model-appropriate `SpeechPlan` of `SpeechSegment`s, its `SentenceChunker` for
sentence-aligned chunk boundaries, and its public async Engine/Session synthesis/playback
contract (`ISpeechSynthesizerEngine`/`ISynthesisSession`), which composes a synthesizer engine
for an installed synthesis model via `LoadAsync` and then a session bound to a playback device
via the engine's `CreateSessionAsync` without ever throwing and plays each synthesized chunk in order
through a public, mockable synthesis-backend seam.

A second, sibling system, `SpeechDemo`, sits alongside `Speech` in the model. It is the Avalonia
desktop application that consumes the library's public API and demonstrates it, and it is
structured with six subsystems: `ShellSubsystem` (the manual composition root and the
capability-navigation window shell), `DeviceSelectionSubsystem` (the capture/playback pickers
over a demo-owned enumeration seam), `ModelCatalogSubsystem` (the catalog listing and download
panel over a demo-owned catalog seam), `ModelSettingsSubsystem` (the generic per-model
tunable-parameter presenters embedded in the panels below), `RecognitionPanelSubsystem` (the
speech-to-text panel over a demo-owned session seam), and `SynthesisPanelSubsystem` (the
text-to-speech panel over a demo-owned session seam, embedding `ModelSettingsSubsystem`). The
dependency runs one way only: `SpeechDemo` references `Speech`, and `Speech` neither references
nor knows about `SpeechDemo`.

A third, sibling system, `SpeechCli`, also sits alongside `Speech` in the model. It is the
cross-platform .NET global tool (`speech-cli`) that exposes the library's capabilities from the
command line, and it is structured with five subsystems: `ModelCommandsSubsystem` (the seven
model-management subcommands - `list-models`, `model-info`, `download`, `uninstall`, `clean`,
`list-voices`, and `voices` - over a CLI-owned catalog seam), `DeviceCommandsSubsystem` (the
three device-related subcommands consuming the library's probe/factory interfaces directly),
`SynthesisCommandSubsystem` (the `speak` subcommand, extending the catalog seam with synthesis
members and adding a CLI-owned playback-device seam), `RecognitionCommandSubsystem` (the `recognize` subcommand and its
silence-timeout
utility, extending the same seam with recognition members), and `ConversationCommandSubsystem`
(the `ask` subcommand, a speak-then-listen conversation turn reusing the synthesis and
recognition subsystems' existing catalog-seam members, and adding only a CLI-owned capture-device seam). As with
`SpeechDemo`, the dependency runs one way only: `SpeechCli` references `Speech`, and `Speech`
neither references nor knows
about `SpeechCli`.

A fourth, sibling system, `SpeechSherpa`, also sits alongside `Speech` in the model. It is the
`DemaConsulting.Speech.Sherpa` library that supplies the concrete sherpa-onnx-backed models and
inference backends this repository ships, and it is structured with three subsystems, each named
after the `Speech` subsystem it extends: `ModelManagementSubsystem` (the `AddSherpaModels()`
catalog extension method, the two recognition models and two synthesis models, and their shared
archive-extraction and transcript-restoration helpers), `RecognitionSubsystem` (the real
sherpa-onnx streaming recognition backend), and `SynthesisSubsystem` (the real sherpa-onnx
offline text-to-speech backend). Unlike `SpeechDemo` and `SpeechCli`, `SpeechSherpa` both consumes
`Speech` and extends it, by implementing the `IRecognitionModel`/`ISynthesisModel` seam whose
`CreateBackend` members return `Speech`'s own engine-neutral backend interfaces. The
dependency still runs one way only: `SpeechSherpa` references `Speech`, and `Speech` neither
references nor knows about `SpeechSherpa`, naming no sherpa-onnx type even internally.

Four further sibling systems extend `Speech` in the same way, each a separately packaged library
that `Speech` neither references nor knows about:

- `SpeechOnnx` (`DemaConsulting.Speech.Onnx`) holds shared ONNX Runtime infrastructure in one
  subsystem, `OnnxRuntimeSubsystem`, whose `OnnxExecutionProviderSelector` creates inference
  sessions with a layered accelerated-then-CPU provider fallback.
- `SpeechOnnxKokoro` (`DemaConsulting.Speech.Onnx.Kokoro`) supplies a Kokoro-82M text-to-speech
  model and engine in two subsystems, `ModelManagementSubsystem` (catalog extension, model, text
  normalizer, lexicon phonemizer, phoneme vocabulary) and `SynthesisSubsystem` (the engine). It
  depends on `Speech` and `SpeechOnnx`.
- `SpeechOnnxNemotronStt` (`DemaConsulting.Speech.Onnx.NemotronStt`) supplies a streaming
  Nemotron speech-recognition model and engine in two subsystems, `ModelManagementSubsystem`
  and `RecognitionSubsystem` (the engine, feature extractor, RNN-T decoder, and silence limiter).
  It depends on `Speech` and `SpeechOnnx`.
- `SpeechOnnxChatterboxTurbo` (`DemaConsulting.Speech.Onnx.ChatterboxTurbo`) supplies a
  voice-cloning text-to-speech model and engine in two subsystems, `ModelManagementSubsystem`
  (catalog extension, model and options, tokenizer, reference voice) and `SynthesisSubsystem`
  (engine, networks, token selector, conditioning, capability profile). It depends on `Speech` and
  `SpeechOnnx`.

These systems implement the public `IRecognitionModel`/`ISynthesisModel` and
`IRecognitionBackend`/`ISynthesisBackend` seams, so they behave as ordinary external consumers of
`Speech`'s public API.

## Folder Layout

The source code folder structure mirrors the software structure organization, with file paths
and descriptions as follows:

```text
src/DemaConsulting.Speech/
├── Diagnostics/
│   ├── ISpeechDiagnostics.cs                  — Contract for reporting structural events
│   ├── NullSpeechDiagnostics.cs               — Default no-op diagnostics sink
│   └── SpeechDiagnosticLevel.cs               — Diagnostic event severity levels
└── AudioSubsystem/
    ├── AudioDeviceDescription.cs              — Immutable description of one audio device
    ├── AudioDeviceSelection.cs                — Persistable device preference and resolution
    ├── IAudioCaptureDevice.cs                 — Audio capture device contract
    ├── IAudioPlaybackDevice.cs                — Audio playback device contract
    ├── IAudioCaptureDeviceProbe.cs            — Capture device enumeration contract
    ├── IAudioPlaybackDeviceProbe.cs           — Playback device enumeration contract
    ├── AudioDeviceFactory.cs                  — Composition entry point for device creation
    ├── PortAudioCaptureDeviceProbe.cs         — Real capture-device enumeration
    ├── PortAudioPlaybackDeviceProbe.cs        — Real playback-device enumeration
    ├── PortAudioCaptureDevice.cs              — Real capture-device implementation
    ├── PortAudioPlaybackDevice.cs             — Real playback-device implementation
    ├── AudioDeviceUnavailableException.cs     — Thrown by unavailable devices when misused
    ├── UnavailableAudioCaptureDevice.cs       — Honest unavailable capture-device fallback
    ├── UnavailableAudioPlaybackDevice.cs      — Honest unavailable playback-device fallback
    ├── UnavailableAudioCaptureDeviceProbe.cs  — Honest unavailable capture-probe fallback
    ├── UnavailableAudioPlaybackDeviceProbe.cs — Honest unavailable playback-probe fallback
    ├── AudioDeviceInUseException.cs           — Thrown when a device is held by another process
    ├── AudioFormat.cs                         — Sample rate/channel/encoding description
    ├── AudioClip.cs                           — In-memory decoded mono/multi-channel audio clip
    ├── AudioFileReader.cs                     — Content-sniffing WAV/FLAC file/stream reader
    ├── AudioFileReadOptions.cs                — Target rate/channels/size options for reading
    ├── WavDecoder.cs                          — Managed RIFF/WAVE decoder
    ├── FlacDecoder.cs                         — Managed FLAC stream decoder
    ├── FlacFrameDecoder.cs                    — FLAC frame/subframe decoding
    ├── FlacBitReader.cs                       — FLAC bit-level reader
    ├── FlacCrc.cs                             — FLAC CRC-8/CRC-16 checks
    ├── AudioResampler.cs                      — Rate conversion for decoded clips
    ├── WindowedSincLowpassFilter.cs           — Windowed-sinc anti-alias filter
    ├── WavFileAudioCaptureDevice.cs           — File-backed capture device (reads a mono 16-bit PCM WAV)
    ├── WavFileAudioPlaybackDevice.cs          — File-backed playback device (writes a WAV)
    └── PortAudio/
        ├── IPortAudioApi.cs                   — Mockable PortAudio runtime seam
        ├── IPortAudioStream.cs                — Mockable PortAudio stream seam
        ├── PortAudioApi.cs                    — Real PortAudioSharp2 + P/Invoke adapter
        ├── PortAudioEnvironment.cs            — Shared init and preferred-host resolver
        ├── PortAudioDeviceInfo.cs             — Managed PortAudio device metadata
        ├── PortAudioHostApiInfo.cs            — Managed PortAudio host-API metadata
        ├── PortAudioHostApiType.cs            — Stable PortAudio host-API identifiers
        └── PortAudioNativeMethods.cs          — Supplementary host-API P/Invoke bindings
```

```text
src/DemaConsulting.Speech/
└── ModelManagementSubsystem/
    ├── SpeechModelStoreOptions.cs             — Host-configurable storage root override
    ├── SpeechModelStore.cs                    — Atomic per-user model storage/install/uninstall
    ├── SpeechModelStoreException.cs           — Thrown when a store operation cannot complete
    ├── DownloadMirror.cs                      — Optional download-URI mirror redirect
    ├── SpeechModelDownloaderOptions.cs        — Downloader tuning options
    ├── NumericParameterBounds.cs              — Shared numeric-parameter bounds validation
    ├── SpeechModelParameterDiagnostics.cs     — Parameter-coercion diagnostics helper
    ├── ReferenceVoiceInfo.cs                  — One listed reference voice (name, source, clips)
    ├── ReferenceVoiceSource.cs                — User-folder vs installed-pack source enum
    ├── ReferenceVoicePack.cs                  — Downloadable reference-voice pack descriptor
    ├── ReferenceVoicePackInstaller.cs         — Pack download/verify/install orchestration
    ├── ReferenceVoiceLibrary.cs               — Lists user-folder and installed-pack voices
    ├── SpeechModelDownloadFile.cs             — One downloadable file's URI/checksum/install path
    ├── SpeechModelDownloadDescriptor.cs       — Ordered set of files required to install a model
    ├── SpeechModelDownloadProgress.cs         — Single-file transfer progress payload
    ├── IModelDownloadClient.cs                — Mockable seam for fetching one file's bytes
    ├── HttpModelDownloadClient.cs             — Real HttpClient-backed download implementation
    ├── SpeechModelDownloader.cs               — Queued fetch-verify-install orchestration
    ├── SpeechModelRole.cs                     — Recognition/Synthesis model role enum
    ├── SpeechModelState.cs                    — Model install lifecycle state enum
    ├── SpeechModelAudioTagSupport.cs          — Declared inline audio-tag support shape enum
    ├── ISpeechModelParameter.cs               — Common tunable-parameter descriptor contract
    ├── NumericParameter.cs                    — Min/max/step/default numeric parameter descriptor
    ├── ChoiceParameter.cs                     — Value/label choice parameter descriptor
    ├── BooleanParameter.cs                    — Boolean parameter descriptor
    ├── ISpeechModel.cs                        — Common per-model contract
    ├── IRecognitionModel.cs                   — Recognition-role marker interface
    ├── ISynthesisModel.cs                     — Synthesis-role marker interface
    ├── SpeechModelDescriptor.cs               — Catalog read-model: a model + its install state
    └── SpeechModelCatalog.cs                  — Known-model enumeration and download orchestration
```

```text
src/DemaConsulting.Speech/
└── RecognitionSubsystem/
    ├── ISpeechRecognizerEngine.cs                — Loaded-model engine contract (Layer 3)
    ├── IRecognitionSession.cs                    — Per-device session contract (Layer 5)
    ├── RecognitionSessionState.cs                — Session state machine enum
    ├── SessionStateChangedEventArgs.cs           — StateChanged event payload
    ├── SpeechRecognitionResult.cs                — Recognized text plus provisional/final flag
    ├── SpeechRecognitionEvent.cs                 — Result-carrying recognition event payload
    ├── SpeechRecognizerUnavailableException.cs   — Thrown by unavailable engines/sessions when misused
    ├── RecognitionEngineBusyException.cs         — Thrown by a concurrent CreateSessionAsync while leased
    ├── RecognitionSessionFaultedException.cs     — Surfaced through GetResultsAsync on worker fault
    ├── SpeechRecognizerFactory.cs                — Composition entry point: LoadAsync returns an engine
    ├── UnavailableSpeechRecognizerEngine.cs       — Honest unavailable engine fallback
    ├── UnavailableRecognitionSession.cs          — Honest unavailable session fallback
    ├── IRecognitionBackend.cs                    — Mockable speech-inference seam (public)
    ├── IRecognitionBackendFactory.cs             — Mockable engine-loading seam (internal)
    ├── DefaultRecognitionBackendFactory.cs       — Real model-driven backend loader
    ├── SpeechRecognizerEngine.cs                 — Real ISpeechRecognizerEngine implementation
    ├── RecognitionSession.cs                     — Real IRecognitionSession streaming pipeline
    ├── RecognitionResultBuffer.cs                — Byte-capped backpressure buffer for GetResultsAsync
    ├── DedicatedWorker.cs                        — Long-running worker thread with cooperative-cancel-then-abandon
    └── AudioFrameResampler.cs                    — Downmix and rate conversion for captured audio
```

```text
src/DemaConsulting.Speech/
└── SynthesisSubsystem/
    ├── NaturalLanguageAudioTag.cs                — Closed vocabulary of canonical tag values
    ├── NaturalLanguageAudioTagKind.cs            — Tag category grouping enum
    ├── AudioTagDescriptor.cs                     — One catalog entry's tag/kind/alias report shape
    ├── AudioTagCatalog.cs                        — Alias-to-tag/kind lookup table + normalization
    ├── TaggedTextSpanKind.cs                     — PlainText/Tag span discriminator
    ├── TaggedTextSpan.cs                         — Neutral parsed-text span value shape
    ├── AudioTagParser.cs                         — Layer 1: bracket-syntax scanner
    ├── SpeechSegment.cs                          — One rendered segment: text, silence, overrides
    ├── SpeechPlan.cs                              — Ordered SpeechSegment sequence (public)
    ├── SpeechParameterConventions.cs             — Tag-to-numeric-parameter mapping conventions
    ├── IModelCapabilityProfile.cs                — Layer 2 rendering strategy contract (public)
    ├── DefaultModelCapabilityProfile.cs          — Generically-correct default rendering strategy
    ├── SentenceChunker.cs                        — Sentence/clause-sized chunking
    ├── BackendAudio.cs                             — Raw engine output: samples plus produced rate (public)
    ├── ISpeechSynthesizerEngine.cs                — Loaded-model engine contract (Layer 3)
    ├── ISynthesisSession.cs                       — Per-device session contract (Layer 5)
    ├── SynthesisSessionState.cs                   — Session state machine enum
    ├── SessionStateChangedEventArgs.cs            — StateChanged event payload
    ├── SpeechSynthesizerUnavailableException.cs   — Thrown by unavailable engines/sessions when misused
    ├── SynthesisEngineBusyException.cs            — Thrown by a concurrent CreateSessionAsync while leased
    ├── SynthesisSessionFaultedException.cs        — Surfaced on worker fault
    ├── SpeechSynthesizerFactory.cs                — Composition entry point: LoadAsync returns an engine
    ├── UnavailableSpeechSynthesizerEngine.cs       — Honest unavailable engine fallback
    ├── UnavailableSynthesisSession.cs             — Honest unavailable session fallback
    ├── ISynthesisBackend.cs                       — Mockable speech-synthesis seam (public)
    ├── ISynthesisBackendFactory.cs                — Mockable engine-loading seam (internal)
    ├── DefaultSynthesisBackendFactory.cs          — Real model-driven backend loader
    ├── SpeechSynthesizerEngine.cs                 — Real ISpeechSynthesizerEngine implementation
    ├── SynthesisSession.cs                        — Real ISynthesisSession chunked synthesis
    ├── DedicatedWorker.cs                         — Long-running worker thread with cooperative-cancel-then-abandon
    ├── PlaybackAudioResampler.cs                  — Rate conversion and upmix for playback audio
    └── SynthesizedSpeech.cs                       — One synthesized segment's audio and silence
```

Several small public types are declared in the file of the type they accompany rather than in a
file of their own, and are documented with that owning unit:

- `AudioDeviceDirection` is declared in `AudioDeviceDescription.cs`.
- `AudioCaptureFrameEventArgs` is declared in `IAudioCaptureDevice.cs`.
- `ChoiceParameterOption` is declared in `ChoiceParameter.cs`.
- `SpeechModelDownloadOutcome` and `SpeechModelDownloadResult` are declared in
  `SpeechModelDownloader.cs`.

Each subsystem folder also holds a `NamespaceDoc.cs` file that carries only the namespace's
XML documentation summary; it declares no behavior and is not a software item.

The SpeechDemo application's folder structure likewise mirrors its software structure:

```text
src/DemaConsulting.Speech.Demo/
├── Program.cs                                 — Process entry point and Avalonia host builder
├── App.axaml / App.axaml.cs                   — Theme and manual composition root
├── AppLaunchOptions.cs                        — Command-line option parsing
├── MirrorOptionsFactory.cs                    — Shared mirror-option validation
├── ShellSubsystem/
│   ├── MainWindow.axaml / .axaml.cs           — Navigation window shell
│   ├── MainWindowViewModel.cs                 — Panel list and selected-panel state
│   └── DemoPanelViewModel.cs                  — One titled navigable panel entry
├── DeviceSelectionSubsystem/
│   ├── IAudioDeviceService.cs                 — Demo-owned device enumeration seam
│   ├── AudioDeviceService.cs                  — Real seam over the library's device probes
│   ├── DeviceSelectionViewModel.cs            — Device picker presentation state
│   └── DeviceSelectionView.axaml / .axaml.cs  — Device picker view
├── ModelCatalogSubsystem/
│   ├── IModelCatalogService.cs                — Demo-owned model catalog seam
│   ├── ModelCatalogService.cs                 — Real seam over the library's model catalog
│   ├── ModelCatalogComposition.cs             — Catalog and reference voice library pair
│   ├── ModelCatalogViewModel.cs               — Catalog panel presentation state
│   ├── ModelListItemViewModel.cs              — One catalog row's presentation state
│   ├── ReferenceVoicePackListItemViewModel.cs — One reference voice pack row's presentation state
│   └── ModelCatalogView.axaml / .axaml.cs     — Catalog panel view
├── ModelSettingsSubsystem/
│   ├── ParameterViewModelBase.cs              — Shared base for one parameter's presentation state
│   ├── NumericParameterViewModel.cs           — Slider/numeric up-down presentation state
│   ├── ChoiceParameterViewModel.cs            — Dropdown presentation state
│   ├── BooleanParameterViewModel.cs           — Checkbox presentation state
│   ├── ModelSettingsViewModel.cs              — Selected model's parameter list and empty state
│   └── ModelSettingsView.axaml / .axaml.cs    — Settings panel view, embedded by other panels
├── RecognitionPanelSubsystem/
│   ├── IRecognizerSessionFactory.cs           — Demo-owned recognizer-session seam
│   ├── RecognizerSessionFactory.cs            — Real seam over SpeechRecognizerFactory
│   ├── RecognitionStreamingState.cs           — Idle/listening/error streaming state enum
│   ├── RecognitionPanelViewModel.cs           — Speech-to-text panel presentation state
│   ├── CaptureDebugRecorder.cs                — Opt-in diagnostic capture-to-WAV recorder
│   ├── WavFileWriter.cs                       — Minimal WAV writer used by the recorder
│   └── RecognitionPanelView.axaml / .axaml.cs — Speech-to-text panel view
└── SynthesisPanelSubsystem/
    ├── ISynthesizerSessionFactory.cs          — Demo-owned synthesizer-session seam
    ├── SynthesizerSessionFactory.cs           — Real seam over SpeechSynthesizerFactory
    ├── SynthesisPlaybackState.cs              — Idle/synthesizing/playing/error playback state enum
    ├── SynthesisPanelViewModel.cs             — Text-to-speech panel presentation state
    └── SynthesisPanelView.axaml / .axaml.cs   — Text-to-speech panel view
```

`Program.cs`, `App.axaml`/`App.axaml.cs`, `AppLaunchOptions.cs` and `MirrorOptionsFactory.cs` sit at
the application root rather than inside a subsystem folder because Avalonia requires the process
entry point and the application class at the application's root namespace. They are units of the
SpeechDemo system itself.

The SpeechCli tool's folder structure likewise mirrors its software structure:

```text
src/DemaConsulting.Speech.Cli/
├── Program.cs                                 — Process entry point, banner, help, dispatch
├── Cli/
│   ├── Context.cs                             — Global-option parsing and per-invocation state
│   └── CommandDispatch.cs                     — Fixed subcommand name-to-handler dispatch table
├── SelfTest/
│   └── Validation.cs                          — CI-safe `--validate` self-check implementation
├── Utilities/
│   └── PathHelpers.cs                         — Shared path-handling helpers
├── Commands/
│   ├── ModelCommandsSubsystem/
│   │   ├── ICliModelCatalog.cs                — CLI-owned catalog seam contract
│   │   ├── SpeechModelCatalogAdapter.cs       — Real seam over SpeechModelCatalog/SpeechModelStore
│   │   ├── CliModelCatalogFactory.cs          — Composition entry point for the catalog seam
│   │   ├── ListModelsCommand.cs               — `list-models` implementation
│   │   ├── ModelInfoCommand.cs                — `model-info` implementation
│   │   ├── DownloadCommand.cs                 — `download` implementation
│   │   ├── UninstallCommand.cs                — `uninstall` implementation
│   │   ├── CleanCommand.cs                    — `clean` implementation
│   │   ├── ListVoicesCommand.cs               — `list-voices` implementation
│   │   └── VoicesCommand.cs                   — `voices` (reference-voice pack) implementation
│   ├── DeviceCommandsSubsystem/
│   │   ├── ListDevicesCommand.cs              — `list-devices` implementation
│   │   ├── DevicesTestCommand.cs              — `devices test` implementation
│   │   └── DoctorCommand.cs                   — `doctor` implementation
│   ├── SynthesisCommandSubsystem/
│   │   ├── SpeakCommand.cs                    — `speak` implementation
│   │   ├── ParameterBagParser.cs              — Shared `--tts-param`/`--stt-param key=value` parsing/validation
│   │   ├── ICliPlaybackDeviceSource.cs        — CLI-owned playback-device seam contract
│   │   └── AudioDeviceFactoryPlaybackDeviceSource.cs — Real seam over AudioDeviceFactory
│   ├── RecognitionCommandSubsystem/
│   │   ├── RecognizeCommand.cs                — `recognize` implementation
│   │   └── SilenceTimeoutRecognizerSession.cs — Mic idle-timeout utility for `recognize --mic`
│   └── ConversationCommandSubsystem/
│       ├── AskCommand.cs                                — `ask` implementation
│       ├── ICliCaptureDeviceSource.cs                   — CLI-owned capture-device seam contract
│       └── AudioDeviceFactoryCaptureDeviceSource.cs     — Real seam over AudioDeviceFactory
```

The SpeechSherpa library's folder structure likewise mirrors its software structure:

```text
src/DemaConsulting.Speech.Sherpa/
├── SpeechModelCatalogSherpaExtensions.cs                  — `AddSherpaModels()` catalog registration
├── ModelManagementSubsystem/
│   ├── SherpaOnnxZipformerEnRecognitionModel.cs           — Streaming Zipformer recognition model
│   ├── SherpaOnnxNemotronStreamingEnRecognitionModel.cs   — Streaming Nemotron recognition model
│   ├── SherpaOnnxVitsLibriTtsEnglishSynthesisModel.cs     — VITS/Piper LibriTTS synthesis model
│   ├── SherpaOnnxKokoroEnglishSynthesisModel.cs           — Kokoro multi-voice synthesis model
│   ├── TarBz2ArchiveExtractor.cs                          — Shared `.tar.bz2` install helper
│   └── UppercaseTranscriptRestorer.cs                     — Casing/punctuation restoration helper
├── RecognitionSubsystem/
│   └── SherpaOnnxRecognitionEngine.cs                     — Real sherpa-onnx streaming backend
└── SynthesisSubsystem/
    └── SherpaOnnxSynthesisEngine.cs                       — Real sherpa-onnx offline-TTS backend
```

`SpeechModelCatalogSherpaExtensions.cs` sits at the library root rather than inside a subsystem
folder because it is the package's public composition entry point in the
`DemaConsulting.Speech.Sherpa` namespace. It belongs to the ModelManagementSubsystem for review
and traceability purposes.

The SpeechOnnxChatterboxTurbo library's folder structure also mirrors its software structure:

```text
src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/
├── SpeechModelCatalogChatterboxTurboExtensions.cs         — `AddChatterboxTurboModels()` registration
├── ModelManagementSubsystem/
│   ├── OnnxChatterboxTurboEnglishSynthesisModel.cs        — Voice-cloning synthesis model
│   ├── ChatterboxTurboModelOptions.cs                     — Voices, variants, and tuning options
│   ├── ChatterboxTurboLanguageModelVariant.cs             — Language-model precision variant enum
│   ├── ChatterboxTurboAuxiliaryModelVariant.cs            — Auxiliary-graph precision variant enum
│   ├── ChatterboxTurboEndOfSentenceTagPolicy.cs           — End-of-sentence tag policy enum
│   ├── ByteLevelBpeTokenizer.cs                           — Embedded byte-level BPE tokenizer
│   └── ChatterboxTurboReferenceVoice.cs                   — Normalized reference clip
└── SynthesisSubsystem/
    ├── OnnxChatterboxTurboSynthesisEngine.cs              — Bounded autoregressive backend
    ├── OnnxChatterboxTurboNetworks.cs                     — Four ONNX Runtime sessions
    ├── IChatterboxTurboNetworks.cs                        — Mockable network seam
    ├── IChatterboxTurboLanguageModelState.cs              — Language-model decode state seam
    ├── ChatterboxTurboConditioning.cs                     — Reference-clip conditioning bundle
    ├── ChatterboxTurboGenerationResult.cs                 — Generation loop result
    ├── ChatterboxTurboTokenSelector.cs                    — Repetition-penalized selection
    ├── ChatterboxTurboTagKind.cs                          — Tag kind enum
    ├── ChatterboxTurboTagTreatment.cs                     — Tag treatment enum
    └── ChatterboxTurboCapabilityProfile.cs                — Audio-tag rendering policy
```

The SpeechOnnx library's folder structure likewise mirrors its software structure:

```text
src/DemaConsulting.Speech.Onnx/
└── OnnxRuntimeSubsystem/
    └── OnnxExecutionProviderSelector.cs                   — Layered execution-provider fallback
```

The SpeechOnnxKokoro library's folder structure likewise mirrors its software structure:

```text
src/DemaConsulting.Speech.Onnx.Kokoro/
├── SpeechModelCatalogKokoroExtensions.cs                  — `AddKokoroModels()` registration
├── ModelManagementSubsystem/
│   ├── OnnxKokoroEnglishSynthesisModel.cs                 — Kokoro multi-voice synthesis model
│   ├── KokoroTextNormalizer.cs                            — Text normalization for the lexicon
│   ├── KokoroLexiconPhonemizer.cs                         — Embedded-lexicon word-to-phoneme
│   └── KokoroPhonemeVocabulary.cs                         — Phoneme-to-token-id vocabulary
└── SynthesisSubsystem/
    └── OnnxKokoroSynthesisEngine.cs                       — Real ONNX Runtime synthesis backend
```

The SpeechOnnxNemotronStt library's folder structure likewise mirrors its software structure:

```text
src/DemaConsulting.Speech.Onnx.NemotronStt/
├── SpeechModelCatalogNemotronSttExtensions.cs             — `AddNemotronSttModels()` registration
├── ModelManagementSubsystem/
│   ├── OnnxNemotronMultilingualRecognitionModel.cs        — Streaming recognition model
│   └── NemotronVocabulary.cs                              — Vocabulary loader
└── RecognitionSubsystem/
    ├── OnnxNemotronRecognitionEngine.cs                   — Real ONNX Runtime recognition backend
    ├── NemotronEngineOptions.cs                           — Engine tuning options
    ├── INemotronEncoder.cs                                — Mockable encoder seam
    ├── IRnntNetwork.cs                                    — Mockable decoder/joint seam
    ├── NemotronEncoder.cs                                 — Cache-aware streaming encoder
    ├── NemotronRnntNetwork.cs                             — Decoder and joint networks
    ├── NemotronFeatureExtractor.cs                        — Streaming log-mel features
    ├── NemotronRnntGreedyDecoder.cs                       — RNN-T greedy decoding
    ├── DitherNoise.cs                                     — Dither noise source
    └── SilenceRunLimiter.cs                               — Leading-silence mitigation
```

## Document Conventions

Throughout this document:

- Class names, method names, property names, and file names appear in `monospace` font.
- The word **shall** denotes a design constraint that the implementation must satisfy.
- Section headings within each unit chapter follow a consistent structure: overview, data
  model, methods/algorithms, and interactions with other units.
- Text tables are used in preference to diagrams, which may not render in all PDF viewers (the
  software structure diagram above is generated from the SysML2 model).

## Companion Artifact Structure

Each software item has corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)
- SysML2 model: `docs/sysml2/model/{system}/.../{item}.sysml` (kebab-case)
- Review-sets: defined in `.reviewmark.yaml`

Each sibling system has its own parallel `{system}` folders in these trees - for example,
SpeechSherpa's artifacts live under `docs/reqstream/speech-sherpa/`, `docs/design/speech-sherpa/`,
`docs/verification/speech-sherpa/`, `src/DemaConsulting.Speech.Sherpa/`, and
`test/DemaConsulting.Speech.Sherpa.Tests/`. SpeechSherpa's subsystems share their names with the
Speech subsystems they extend, so each subsystem is always identified together with its system.

## References

- Speech User Guide — the compiled User Guide document for this repository.
- Speech Repository — the Speech source repository hosted on GitHub.
