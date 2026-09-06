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
items, and of the SpeechDemo application system and its constituent software items,
specifically:

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
  and its role-marker interfaces, and catalog enumeration of the compiled-in known-model
  registry alongside install state
- **RecognitionSubsystem (Subsystem)** — Streaming speech-to-text: the public recognizer
  contract and result types, the composition root with honest unavailable fallback, the
  capture-to-engine pipeline with its audio-format converter, and the mockable
  recognition-engine seam
- **SynthesisSubsystem (Subsystem)** — Text-to-speech: the closed, fixed Natural Language Audio
  Tag vocabulary grouped into kinds and the model-independent Layer 1 parser that recognizes
  bracket syntax against it, the Layer 2 rendering strategy that turns a parsed span sequence
  into a model-appropriate `SpeechPlan`, sentence chunking for pipelined synthesis, the public
  `ISpeechSynthesizer` streaming/playback contract with its composition root and honest
  unavailable fallback, the mockable synthesis-engine seam, the real sherpa-onnx synthesis
  engine, and the playback-format converter

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

The following OTS items are also covered:

- **PortAudioSharp2** — managed PortAudio binding and transitive native runtime carrier
- **SherpaOnnx** — managed local speech-inference API and transitive native runtime carrier
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
`ISynthesisModel` contracts; the compiled-in catalog (`SpeechModelCatalog.KnownModels`) ships
four concrete models covering both roles - two recognition models
(`SherpaOnnxZipformerEnRecognitionModel`, `SherpaOnnxNemotronStreamingEnRecognitionModel`) and
two synthesis models (`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`,
`SherpaOnnxKokoroEnglishSynthesisModel`) - so a host may use the compiled-in registry directly
or supply its own. A fourth subsystem,
`RecognitionSubsystem`, consumes both of the above: it defines the public streaming
speech-to-text contract, composes a recognizer for an installed recognition model and a capture
device without ever throwing, and converts captured audio into the format a model requires before
streaming it through an internal, mockable recognition-engine seam. A fifth subsystem,
`SynthesisSubsystem`, provides the text-to-speech side: its closed, fixed Natural Language Audio
Tag vocabulary and the model-independent Layer 1 parser (`AudioTagCatalog`/`AudioTagParser`) that
recognizes bracket syntax against it, its Layer 2 rendering strategy
(`IModelCapabilityProfile`/`DefaultModelCapabilityProfile`) that turns a parsed span sequence
into a model-appropriate `SpeechPlan` of `SpeechSegment`s, its `SentenceChunker` for
pipeline-friendly chunk boundaries, and its public `ISpeechSynthesizer` streaming/playback
contract, which composes a synthesizer for an installed synthesis model and a playback device
without ever throwing and pipelines chunked synthesis with playback through an internal, mockable
synthesis-engine seam.

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
    ├── ISpeechRecognizer.cs                     — Streaming speech-to-text contract
    ├── SpeechRecognitionResult.cs               — Recognized text plus provisional/final flag
    ├── SpeechRecognitionEvent.cs                — Result-carrying recognition event payload
    ├── SpeechRecognizerUnavailableException.cs  — Thrown by unavailable recognizers when misused
    ├── SpeechRecognizerFactory.cs               — Composition entry point for recognizer creation
    ├── UnavailableSpeechRecognizer.cs           — Honest unavailable recognizer fallback
    ├── IRecognitionEngine.cs                    — Mockable speech-inference seam
    ├── IRecognitionEngineFactory.cs             — Mockable engine-loading seam
    ├── SherpaOnnxRecognitionEngine.cs           — Real sherpa-onnx streaming engine adapter
    ├── SherpaOnnxRecognitionEngineFactory.cs    — Real model-driven engine loader
    ├── AudioFrameResampler.cs                   — Downmix and rate conversion for captured audio
    └── SherpaOnnxSpeechRecognizer.cs            — Real streaming recognition pipeline
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
    ├── ISynthesisEngine.cs                        — Mockable speech-synthesis seam
    ├── ISynthesisEngineFactory.cs                 — Mockable engine-loading seam
    ├── SherpaOnnxSynthesisEngine.cs               — Real sherpa-onnx offline-TTS engine adapter
    ├── SherpaOnnxSynthesisEngineFactory.cs        — Real model-driven engine loader
    ├── PlaybackAudioResampler.cs                  — Rate conversion and upmix for playback audio
    ├── SynthesizedSpeech.cs                       — One synthesized segment's audio and silence
    ├── ISpeechSynthesizer.cs                      — Chunked/streaming synthesis-and-playback contract
    ├── SpeechSynthesizerUnavailableException.cs   — Thrown by unavailable synthesizers when misused
    ├── SpeechSynthesizerFactory.cs                — Composition entry point for synthesizer creation
    ├── UnavailableSpeechSynthesizer.cs            — Honest unavailable synthesizer fallback
    └── SherpaOnnxSpeechSynthesizer.cs             — Real chunked/pipelined synthesis pipeline
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

## References

- Speech User Guide — the compiled User Guide document for this repository.
- Speech Repository — the Speech source repository hosted on GitHub.
