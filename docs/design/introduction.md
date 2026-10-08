<!-- cspell:ignore ALSA portaudio -->
# Introduction

This document provides the detailed design for the Speech, a cross-platform .NET
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
SpeechCli command-line tool system and its constituent software items, and of the SpeechSherpa
model-provider library system and its constituent software items, specifically:

- **Speech (System)** — The complete .NET library system providing speech capture, recognition,
  and synthesis capabilities to host applications
- **Diagnostics (Subsystem)** — Reports structural diagnostic events to an optional
  host-supplied sink
- **AudioSubsystem (Subsystem)** — Audio capture/playback contracts, name-based selection,
  honest unavailable fallbacks, and real PortAudio-backed device/probe implementations
- **PortAudio (Subsystem)** — Internal child subsystem under AudioSubsystem that isolates
  PortAudioSharp2 and supplementary host-API P/Invoke bindings behind mockable abstractions
- **ModelManagementSubsystem (Subsystem)** — Per-user speech model storage with atomic
  install/repair/uninstall, the queued download-verify-install orchestration and mockable
  HTTP seam that fetch and install a model's files with honest failure states, and the model
  catalog/contract seam: typed tunable-parameter descriptors, the common per-model contract
  and its role-marker interfaces (the extension point concrete models implement), and catalog
  enumeration of the host-registered known-model list alongside install state
- **RecognitionSubsystem (Subsystem)** — Streaming speech-to-text: the public async
  Engine/Session recognizer contract and result types, the composition root (`LoadAsync`/
  `CreateSessionAsync`) with honest unavailable fallbacks and engine exclusivity lease, the
  capture-to-engine pipeline with its audio-format converter, and the mockable internal
  recognition-backend seam with its model-driven default backend factory
- **SynthesisSubsystem (Subsystem)** — Text-to-speech: the closed, fixed Natural Language Audio
  Tag vocabulary grouped into kinds and the model-independent Layer 1 parser that recognizes
  bracket syntax against it, the Layer 2 rendering strategy that turns a parsed span sequence
  into a model-appropriate `SpeechPlan`, sentence chunking for pipelined synthesis, the public
  async Engine/Session streaming/playback contract with its composition root and honest
  unavailable fallbacks, the mockable internal synthesis-backend seam with its model-driven
  default backend factory, and the playback-format converter

The following software items of the SpeechDemo system are also covered:

- **SpeechDemo (System)** — The Avalonia desktop application that demonstrates the Speech
  library from the position of an ordinary consumer, using only its public API. It is a sibling
  system to Speech, not a subsystem of it: it is separately built and separately run, has its
  own users and its own externally visible behavior, and the library must never depend on it
- **ShellSubsystem (Subsystem)** — The application's manual composition root and window shell,
  presenting one titled, selectable panel per demonstrated capability
- **DeviceSelectionSubsystem (Subsystem)** — The capture and playback device pickers: a
  demo-owned enumeration seam over the library's device probes plus the presentation state that
  lists devices, tracks the chosen device by name, and explains an empty machine honestly
- **ModelCatalogSubsystem (Subsystem)** — The model catalog and download panel: a demo-owned
  catalog seam over the library's model catalog plus the row presentation, download progress and
  honest failure reporting, and the explanatory empty-catalog message
- **ModelSettingsSubsystem (Subsystem)** — The generic per-model tunable-parameter settings
  panel: presenters for the library's numeric, choice, and boolean parameter kinds, chosen by
  concrete type alone, plus the honest empty state shown with no model selected or a model
  declaring no parameters
- **RecognitionPanelSubsystem (Subsystem)** — The speech-to-text panel: a demo-owned session
  seam over the library's `SpeechRecognizerFactory` narrowing a public model to the library's
  recognition role, and the Start/Stop streaming lifecycle with progressive partial-then-final
  transcript rendering and honest reporting of every unavailable state
- **SynthesisPanelSubsystem (Subsystem)** — The text-to-speech panel: a demo-owned session seam
  over the library's `SpeechSynthesizerFactory` narrowing a public model to the library's
  synthesis role, the embedded `ModelSettingsSubsystem` panel for the selected model's
  parameters, example Natural Language Audio Tag hints, and the Play/Stop lifecycle with honest
  reporting of every unavailable state

The following software items of the SpeechCli system are also covered:

- **SpeechCli (System)** — A cross-platform .NET global tool, packaged as
  `DemaConsulting.Speech.Cli` and installed under the command name `speech-cli`, that exposes
  the library's model management, audio device inspection, text-to-speech, and speech-to-text
  capabilities from the command line. It is a sibling system to Speech, not a subsystem of it: it
  is separately built, separately packaged, and separately versioned, has its own users
  (command-line operators and scripts), and the library must never depend on it
- **ModelCommandsSubsystem (Subsystem)** — The five model-management subcommands (`list-models`,
  `model-info`, `download`, `uninstall`, `clean`) and the CLI-owned catalog seam
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
  and `RecognitionCommandSubsystem`'s existing seam members rather than introducing a new one

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
  implementing Speech's internal recognition-backend seam, including its per-model post-endpoint
  warm-up replay mitigation
- **SynthesisSubsystem (Subsystem)** — The real sherpa-onnx offline text-to-speech engine
  implementing Speech's internal synthesis-backend seam

The following OTS items are also covered:

- **PortAudioSharp2** — managed PortAudio binding and transitive native runtime carrier
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

This system is structured with two subsystems directly under the system level: `Diagnostics`
and `AudioSubsystem`. `AudioSubsystem` now contains both public audio-device contracts and
real PortAudio-backed implementations. It also contains a child `PortAudio` subsystem that
isolates the PortAudioSharp2 wrapper and the supplementary host-API P/Invoke bindings behind
internal, mockable abstractions. A third subsystem, `ModelManagementSubsystem`, provides
per-user speech model storage with atomic install/repair/uninstall, a queued
download-verify-install orchestration with a mockable HTTP seam, and the model catalog/
contract seam (typed tunable parameters, the common model contract, and catalog enumeration)
that the RecognitionSubsystem and SynthesisSubsystem consume via the `IRecognitionModel`/
`ISynthesisModel` contracts. Those contracts are the library's model extension point: the
library ships zero built-in models, and a host registers concrete models with the catalog
through `SpeechModelCatalog.AddModels(...)`. A fourth subsystem,
`RecognitionSubsystem`, consumes both of the above: it defines the public async Engine/Session
streaming speech-to-text contract (`ISpeechRecognizerEngine`/`IRecognitionSession`), composing a
recognizer engine for an installed recognition model via `LoadAsync` and then a session bound to
a capture device via the engine's `CreateSessionAsync` without ever throwing for an ordinary
machine state, and converts captured audio into the format a model requires before streaming it
through an internal, mockable recognition-backend seam. A fifth subsystem,
`SynthesisSubsystem`, provides the text-to-speech side: its closed, fixed Natural Language Audio
Tag vocabulary and the model-independent Layer 1 parser (`AudioTagCatalog`/`AudioTagParser`) that
recognizes bracket syntax against it, its Layer 2 rendering strategy
(`IModelCapabilityProfile`/`DefaultModelCapabilityProfile`) that turns a parsed span sequence
into a model-appropriate `SpeechPlan` of `SpeechSegment`s, its `SentenceChunker` for
pipeline-friendly chunk boundaries, and its public async Engine/Session streaming/playback
contract (`ISpeechSynthesizerEngine`/`ISynthesisSession`), which composes a synthesizer engine
for an installed synthesis model via `LoadAsync` and then a session bound to a playback device
via the engine's `CreateSessionAsync` without ever throwing and pipelines chunked synthesis with
playback through an internal, mockable synthesis-backend seam.

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
command line, and it is structured with five subsystems: `ModelCommandsSubsystem` (the five
model-management subcommands over a CLI-owned catalog seam), `DeviceCommandsSubsystem` (the
three device-related subcommands consuming the library's probe/factory interfaces directly),
`SynthesisCommandSubsystem` (the `speak` subcommand, extending the catalog seam with synthesis
members), `RecognitionCommandSubsystem` (the `recognize` subcommand and its silence-timeout
utility, extending the same seam with recognition members), and `ConversationCommandSubsystem`
(the `ask` subcommand, a speak-then-listen conversation turn reusing the synthesis and
recognition subsystems' existing seam members rather than introducing a new one). As with
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
internal `CreateBackend` members return `Speech`'s own engine-neutral backend interfaces. The
dependency still runs one way only: `SpeechSherpa` references `Speech`, and `Speech` neither
references nor knows about `SpeechSherpa`, naming no sherpa-onnx type even internally.

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
    ├── IRecognitionBackend.cs                    — Mockable speech-inference seam (internal)
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
    ├── SpeechPlan.cs                              — Ordered SpeechSegment sequence
    ├── SpeechParameterConventions.cs             — Tag-to-numeric-parameter mapping conventions
    ├── IModelCapabilityProfile.cs                — Layer 2 rendering strategy contract
    ├── DefaultModelCapabilityProfile.cs          — Generically-correct default rendering strategy
    ├── SentenceChunker.cs                        — Sentence/clause-sized chunking for pipelining
    ├── EngineAudio.cs                             — Raw engine output: samples plus produced rate
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
    ├── ISynthesisBackend.cs                       — Mockable speech-synthesis seam (internal)
    ├── ISynthesisBackendFactory.cs                — Mockable engine-loading seam (internal)
    ├── DefaultSynthesisBackendFactory.cs          — Real model-driven backend loader
    ├── SpeechSynthesizerEngine.cs                 — Real ISpeechSynthesizerEngine implementation
    ├── SynthesisSession.cs                        — Real ISynthesisSession chunked/pipelined synthesis
    ├── DedicatedWorker.cs                         — Long-running worker thread with cooperative-cancel-then-abandon
    ├── PlaybackAudioResampler.cs                  — Rate conversion and upmix for playback audio
    └── SynthesizedSpeech.cs                       — One synthesized segment's audio and silence
```

The SpeechDemo application's folder structure likewise mirrors its software structure:

```text
src/DemaConsulting.Speech.Demo/
├── Program.cs                                 — Process entry point and Avalonia host builder
├── App.axaml / App.axaml.cs                   — Theme and manual composition root
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
│   ├── ModelCatalogViewModel.cs               — Catalog panel presentation state
│   ├── ModelListItemViewModel.cs              — One catalog row's presentation state
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
│   └── RecognitionPanelView.axaml / .axaml.cs — Speech-to-text panel view
└── SynthesisPanelSubsystem/
    ├── ISynthesizerSessionFactory.cs          — Demo-owned synthesizer-session seam
    ├── SynthesizerSessionFactory.cs           — Real seam over SpeechSynthesizerFactory
    ├── SynthesisPlaybackState.cs              — Idle/synthesizing/playing/error playback state enum
    ├── SynthesisPanelViewModel.cs             — Text-to-speech panel presentation state
    └── SynthesisPanelView.axaml / .axaml.cs   — Text-to-speech panel view
```

`Program.cs` and `App.axaml`/`App.axaml.cs` sit at the application root rather than inside a
subsystem folder because Avalonia requires the process entry point and the application class at
the application's root namespace. Both belong to the ShellSubsystem for review and traceability
purposes.

The SpeechCli tool's folder structure likewise mirrors its software structure:

```text
src/DemaConsulting.Speech.Cli/
├── Program.cs                                 — Process entry point, banner, help, dispatch
├── Cli/
│   ├── Context.cs                             — Global-option parsing and per-invocation state
│   ├── CommandDispatch.cs                     — Fixed subcommand name-to-handler dispatch table
│   └── ParameterBagParser.cs                  — Shared `--tts-param`/`--stt-param key=value` parsing/validation
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
│   │   └── CleanCommand.cs                    — `clean` implementation
│   ├── DeviceCommandsSubsystem/
│   │   ├── ListDevicesCommand.cs              — `list-devices` implementation
│   │   ├── DevicesTestCommand.cs              — `devices test` implementation
│   │   └── DoctorCommand.cs                   — `doctor` implementation
│   ├── SynthesisCommandSubsystem/
│   │   └── SpeakCommand.cs                    — `speak` implementation
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

## Document Conventions

Throughout this document:

- Class names, method names, property names, and file names appear in `monospace` font.
- The word **shall** denotes a design constraint that the implementation must satisfy.
- Section headings within each unit chapter follow a consistent structure: overview, data
  model, methods/algorithms, and interactions with other units.
- Text tables are used in preference to diagrams, which may not render in all PDF viewers.

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
