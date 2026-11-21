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
model-provider library system and its constituent software items, of the five ONNX
Runtime-based sibling library systems (SpeechOnnx, SpeechOnnxKokoro, SpeechOnnxPiper,
SpeechOnnxNemotronStt, and SpeechOnnxChatterboxTurbo) and their constituent software items, of the
SpeechTextEn English text-analysis library system and its constituent software items, and of the
SpeechPhonemesEn English phonemizer library system and its constituent software items, specifically:

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
  into a model-appropriate `SpeechPlan`, sentence chunking for sequential chunked synthesis, the
  language-neutral text-analysis seam (annotations, tag-placeholder analysis text, and
  analyzer-supplied sentence boundaries) and the optional annotated backend seam, the public
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
  library's `SpeechModelCatalog`/`SpeechModelStore`; units `ICliModelCatalog`,
  `SpeechModelCatalogAdapter`, `CliModelCatalogFactory`, `DownloadProgressReporter`, `ListModelsCommand`,
  `ModelInfoCommand`, `DownloadCommand`, `UninstallCommand`, `CleanCommand`, `ListVoicesCommand` and
  `VoicesCommand`
- **DeviceCommandsSubsystem (Subsystem)** — The three device-related subcommands
  (`list-devices`, `devices test`, `doctor`), consuming the library's already-public
  `IAudioCaptureDeviceProbe`/`IAudioPlaybackDeviceProbe`/`AudioDeviceFactory` directly, with no
  CLI-owned seam wrapper; units `ListDevicesCommand`, `DevicesTestCommand` and `DoctorCommand`
- **SynthesisCommandSubsystem (Subsystem)** — The one text-to-speech subcommand (`speak`),
  extending `ModelCommandsSubsystem`'s `ICliModelCatalog` seam with synthesis-side members
  rather than introducing a second, competing seam, and adding one CLI-owned playback-device
  seam (`ICliPlaybackDeviceSource`); units `SpeakCommand`, `ParameterBagParser`,
  `ICliPlaybackDeviceSource` and `AudioDeviceFactoryPlaybackDeviceSource`
- **RecognitionCommandSubsystem (Subsystem)** — The one speech-to-text subcommand
  (`recognize`) plus the `SilenceTimeoutRecognizerSession` idle-timeout utility, extending
  `ModelCommandsSubsystem`'s `ICliModelCatalog` seam with recognition-side members symmetric to
  the synthesis pair; units `RecognizeCommand` and `SilenceTimeoutRecognizerSession`
- **ConversationCommandSubsystem (Subsystem)** — The one voice-conversation subcommand (`ask`),
  running a speak-then-listen turn as one invocation by reusing `SynthesisCommandSubsystem`'s
  and `RecognitionCommandSubsystem`'s existing `ICliModelCatalog` seam members (no new catalog
  member) and adding one CLI-owned capture-device seam (`ICliCaptureDeviceSource`); units `AskCommand`,
  `ICliCaptureDeviceSource` and `AudioDeviceFactoryCaptureDeviceSource`
- **Program, CommandDispatch, Context, ConsoleCancellation, Validation, PathHelpers (Units)** — The
  units that belong directly to SpeechCli rather than to a command subsystem: the process entry
  point (`Program`, including `--version`/`--help`/`--validate` priority handling), the fixed
  subcommand dispatch table, global-option parsing and per-invocation state, Ctrl+C handling, the
  CI-safe `--validate` self-check, and shared path helpers

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

The following software items of the five ONNX Runtime-based sibling systems are also covered.
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
  implementing Speech's `ISynthesisModel`/`ISynthesisBackend` seam, which turns text into phoneme
  token ids with the English text analysis of SpeechTextEn, the phonemizer and Kokoro renderer of
  SpeechPhonemesEn, and its own embedded phoneme vocabulary
- **ModelManagementSubsystem (Subsystem of SpeechOnnxKokoro)** — The `AddKokoroModels()` catalog
  extension, the Kokoro model class (with the shared English text analyzer it hands to the session),
  and the phoneme vocabulary unit
- **SynthesisSubsystem (Subsystem of SpeechOnnxKokoro)** — `OnnxKokoroSynthesisEngine`, the real
  synthesis backend, and `KokoroPhonemePipeline`, which turns text and annotations into phoneme
  token ids in parts of bounded length
- **SpeechOnnxPiper (System)** — The `DemaConsulting.Speech.Onnx.Piper` library, a concrete ONNX
  Runtime-backed Piper (VITS) text-to-speech model and engine for the English (US) `libritts_r`
  medium voice (904 speakers, 22,050 Hz) built on SpeechOnnx and implementing Speech's
  `ISynthesisModel`/`ISynthesisBackend` seam, which turns text into phoneme ids with the English text
  analysis of SpeechTextEn, the phonemizer and espeak-style IPA renderer of SpeechPhonemesEn, and the
  voice's own symbol table, with no espeak-ng
- **ModelManagementSubsystem (Subsystem of SpeechOnnxPiper)** — The `AddPiperModels()` catalog
  extension, the Piper voice model class with its internal archive extractor, and the voice
  configuration reader `PiperVoiceConfig`
- **SynthesisSubsystem (Subsystem of SpeechOnnxPiper)** — `OnnxPiperSynthesisEngine`, the real
  synthesis backend, and `PiperPhonemePipeline`, which turns text and annotations into framed
  phoneme ids in parts of bounded length
- **SpeechOnnxNemotronStt (System)** — The `DemaConsulting.Speech.Onnx.NemotronStt` library, a
  concrete ONNX Runtime-backed streaming speech-recognition model and engine for NVIDIA Nemotron
  built on SpeechOnnx and implementing Speech's `IRecognitionModel`/`IRecognitionBackend` seam
- **ModelManagementSubsystem (Subsystem of SpeechOnnxNemotronStt)** — The `AddNemotronSttModels()`
  catalog extension, the Nemotron recognition model class, and the vocabulary loader
- **RecognitionSubsystem (Subsystem of SpeechOnnxNemotronStt)** — `OnnxNemotronRecognitionEngine`
  with its streaming feature extractor, cache-aware encoder (`NemotronEncoder`), decoder and joint
  network (`NemotronRnntNetwork`), RNN-T greedy decoder, and silence-run limiter
- **SpeechOnnxChatterboxTurbo (System)** — The `DemaConsulting.Speech.Onnx.ChatterboxTurbo`
  library, a concrete ONNX Runtime-backed voice-cloning text-to-speech model and engine
  implementing Speech's `ISynthesisModel`/`ISynthesisBackend` seam, with its own embedded
  tokenizer, generation loop, and audio-tag rendering policy
- **ModelManagementSubsystem (Subsystem of SpeechOnnxChatterboxTurbo)** — The
  `AddChatterboxTurboModels()` catalog extension, the synthesis model, the byte-level BPE
  tokenizer, and the reference-voice type
- **SynthesisSubsystem (Subsystem of SpeechOnnxChatterboxTurbo)** — The synthesis engine, ONNX
  networks, token selector, reference-clip conditioning, and capability profile

The following software items of the SpeechTextEn system are also covered. It is a sibling system to
Speech, not a subsystem of it, and the library must never depend on it:

- **SpeechTextEn (System)** — The `DemaConsulting.Speech.Text.En` library (a preview), an English implementation of
  Speech's `ITextAnalyzer` seam that annotates the words of a speech request with their Universal Dependencies part of
  speech, describes with replacement-span annotations how English text (abbreviations, symbols, apostrophes, acronyms
  and initialisms, web addresses, e-mail addresses, letter-digit identifiers, and en-US numbers, currency, units, times
  and dates) is spoken, marks letter sequences to be spoken by letter name, lets a host override how individual words
  are spoken, pronounced or read (which reading of a heteronym such as `record` is meant), assigns heteronym reading ids
  with a confidence from an embedded trained model, and supplies authoritative sentence boundaries reconciled with that
  normalization, built on the `DemaConsulting.PartOfSpeech` OTS package and with no inference-runtime dependency
- **AnalysisSubsystem (Subsystem of SpeechTextEn)** — The `EnglishTextAnalyzer` analyzer with its
  detailed-result types, the `EnglishTextAnalyzerOptions` options, the `TextEnAnnotations`
  vocabulary and typed readers, the `PronunciationOverrides` dictionary of host-supplied
  pronunciation overrides (with its `PronunciationOverrideEntry` record, `OverrideAction` and
  `PronunciationScheme` enumerations and the internal `OverrideSyntax` grammars), the internal
  `PartOfSpeechAdapter` seam over the tagging package, and
  the internal `AnalysisDiagnostics` counts-only reporter. It depends on the NormalizationSubsystem and the
  HeteronymSubsystem, never the reverse
- **HeteronymSubsystem (Subsystem of SpeechTextEn)** — The public `ReadingInventory` of heteronym readings with its
  embedded reading and form tables, the internal `HeteronymFeatureExtractor` that turns a tagged sentence into
  deterministic hashed features (with the `HeteronymContext` and `FeatureHashing` helpers), the internal
  `SparsePerceptronModel` with its strict loader (and the `SparsePerceptronModelWriter` and `ModelBinaryFormat`
  helpers), and the internal `HeteronymClassifier` that picks a candidate reading with a calibrated confidence
  (with the lazy `HeteronymModelProvider`); it has no dependency on the other subsystems
- **NormalizationSubsystem (Subsystem of SpeechTextEn)** — The alignment model with its public
  `NormalizationKind` and `CliticKind` enumerations (`NormalizationAlignment`), the internal
  `TextNormalizer` orchestrator, the internal `NumericExpressionRecognizer` that finds numeric
  expressions and decides how each token of one is spoken, the internal `NumberVerbalizer` that builds
  the English words of a number, the internal `AbbreviationResolver` with its embedded abbreviation
  lexicon, the internal `AcronymRecognizer` with its embedded initialism lexicon, the internal
  `WebAddressRecognizer` that speaks URLs, e-mail addresses and bare domains, and the internal
  `AlphanumericTokenRecognizer` that spells letter-digit identifiers such as `A4` or `MP3`
- **TrainingDataSubsystem (Subsystem of SpeechTextEn)** — The non-shipped `check-data` command of the
  `DemaConsulting.Speech.Text.En.Trainer` tool, which verifies the training and evaluation data in `data/text-en/`: the
  internal `TsvTable` strict file reader, the `TextEnDataSet` typed loader with its source vocabulary, the
  `DataManifest` verifier, the `SpanValidator` whole-word span check, the `CheckReport` finding collector, the
  `SchemaChecker`, `LicenseChecker`, `LeakageChecker`, `EvaluationOverlayChecker` and `ClusterDataChecker` checks, and
  the `CheckDataCommand` entry (with the `Program.cs` glue)
- **TrainingSubsystem (Subsystem of SpeechTextEn)** — The non-shipped `train` and `evaluate` commands of the same
  `DemaConsulting.Speech.Text.En.Trainer` tool, which train the heteronym model from the training and development
  data, verify a committed model by retraining, and measure a model end to end on the development and holdout sets with
  the library's own internals (through `InternalsVisibleTo`): the `TrainingCorpus` loader, the `DeterministicRandom` and
  `DeterministicMath` helpers, the `AveragedPerceptronTrainer`, the `ModelSelection` of the epoch and temperature, the
  `TrainingRecord` of the settings and results, the `TrainCommand` entry, the `HeteronymEvaluator` with the quality
  gate and the overlay evaluation with and without the curated cue priors, the `EvaluationOverlay` loader of the
  regression overlay and the generalization set, the `EvaluationRecord` files and the `EvaluateCommand` entry (with the
  `Program.cs` glue)
- **ExternalEvaluationSubsystem (Subsystem of SpeechTextEn)** — The non-shipped `evaluate-external` command of the same
  `DemaConsulting.Speech.Text.En.Trainer` tool, which measures the embedded heteronym model on external,
  evaluation-only datasets (Google WikipediaHomographData and heteronym-bench) fetched at evaluation time from pinned
  revisions into a cache that is never committed: the `ExternalSourceCache` of the pins and the verified cache, the
  `ExternalRowConversion` of offsets, labels and rows, the `WikipediaHomographAdapter` and `HeteronymBenchAdapter`
  readers, the `ExternalEvaluationRecord` files and the `EvaluateExternalCommand` entry (with the `Program.cs` glue)
- **ClusteringSubsystem (Subsystem of SpeechTextEn)** — The non-shipped `cluster` command of the same
  `DemaConsulting.Speech.Text.En.Trainer` tool, which induces the word-cluster table from a pinned corpus of US
  public-domain books fetched from an official mirror: the `GutenbergBody` extraction, the `ClusterCorpusCache` of the
  source list, the rate-limited fetch and the verified bodies, the `CorpusSnapshot` archive, the `CorpusLeakageAudit`
  against every evaluation set, the `ClusterCorpusReader` with the analyzer's tokenizer, the deterministic
  `ClusterInducer`, the `WordClusterTable`, the `ClusteringRecord` and the `ClusterCommand` entry (with the
  `Program.cs` glue)

The following software items of the SpeechPhonemesEn system are also covered. It is a sibling system to Speech, not a
subsystem of it, and the library must never depend on it:

- **SpeechPhonemesEn (System)** — The `DemaConsulting.Speech.Phonemes.En` library (a preview), which turns English text
  and the annotations of SpeechTextEn into the phoneme input of a voice model: an `EnglishPhonemizer` that gives every
  word an ARPAbet pronunciation from the embedded CMU Pronouncing Dictionary, the reading tables, the morphology rules
  or an embedded trained letter-to-sound model, and renderers that write the result in the Piper (espeak-style IPA) or
  the Kokoro (misaki-style) symbols. It is plain managed code over embedded data, with no inference runtime, and is
  used only by the Kokoro and Piper model packages
- **LexiconSubsystem (Subsystem of SpeechPhonemesEn)** — The pronunciation data and the laws over it: the internal
  `CmuLexicon` with its binary image format, the `Phone`/`Pronunciation` value types with the letter names,
  `Morphology` (stem-derived and clitic pronunciations) and `ReadingPronunciations` (the pronunciation of every reading
  id of SpeechTextEn)
- **LetterToSoundSubsystem (Subsystem of SpeechPhonemesEn)** — The pronunciation of a word that no dictionary knows:
  the internal `LtsModel` (an integer-only trained network and its image) and `LetterToSound` (the caching converter
  that never leaves a word silent)
- **PhonemizerSubsystem (Subsystem of SpeechPhonemesEn)** — The public `EnglishPhonemizer` with its `PhonemizedText`
  and `PhonemizedItem` results, the internal `WordTokenizer` (with the spoken-text and annotation index helpers) and
  the internal `WordPronouncer` with its priority list for one word
- **RenderingSubsystem (Subsystem of SpeechPhonemesEn)** — The public `EspeakIpaRenderer` (Piper) and `MisakiRenderer`
  (Kokoro), the internal `WeakForms` function-word table and the internal `PhonemeStream` layout and long-text
  splitting that both renderers share

The following OTS items are also covered:

- **PortAudioSharp2** — managed PortAudio binding and transitive native runtime carrier
- **System.Numerics.Tensors** — vectorized `TensorPrimitives` used by `WavFileAudioCaptureDevice`,
  `SynthesisSession`, and `WindowedSincLowpassFilter`
- **Microsoft.ML.OnnxRuntime** — managed ONNX Runtime API used by the SpeechOnnx family of
  libraries, including the Kokoro and Piper model packages
- **SherpaOnnx** — managed local speech-inference API used by SpeechSherpa
- **SharpCompress** — managed `.tar.bz2` archive reader used by SpeechSherpa and SpeechOnnxPiper model installs
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
- **TestResults** (`DemaConsulting.TestResults`) — TRX and JUnit serializer used by SpeechCli's
  `--validate --results` self-validation output
- **PartOfSpeech** (`DemaConsulting.PartOfSpeech`) — pre-release English tokenizer and
  part-of-speech tagger, with its own embedded model, used by SpeechTextEn
- **VersionMark** — tool-version documentation tool
- **WeasyPrint** — HTML-to-PDF conversion tool
- **xUnit** — unit-testing framework

Version applicability: This design applies to all versions of the Speech.

The following topics are explicitly excluded from this design documentation:

- External library internals beyond the integration points used by this repository
- Build pipeline configuration and CI/CD processes (the wiring of the `check-data` command into the lint script and
  the build job is described with the subsystem, but the pipeline files are not documented here)
- Deployment and distribution mechanisms (the packaging mechanism itself is not specified; a
  packaging constraint that shapes a design, such as SpeechCli's `net10.0`-only target, is
  stated only where it applies)
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
`AudioClip`, and the windowed-sinc `AudioResampler`, which uses its own scalar sinc kernel). It also
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
sentence-aligned chunk boundaries, its language-neutral text-analysis seam (annotations, the
tag-placeholder analysis text, and analyzer-supplied sentence boundaries) together with the
optional annotated backend seam (`IAnnotatedSynthesisBackend`), and its public async Engine/Session synthesis/playback
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

Five further sibling systems extend `Speech` in the same way, each a separately packaged library
that `Speech` neither references nor knows about, and two more, `SpeechTextEn` and
`SpeechPhonemesEn`, supply the English language processing to the Kokoro and Piper packages:

- `SpeechOnnx` (`DemaConsulting.Speech.Onnx`) holds shared ONNX Runtime infrastructure in one
  subsystem, `OnnxRuntimeSubsystem`, whose `OnnxExecutionProviderSelector` creates inference
  sessions with a layered accelerated-then-CPU provider fallback.
- `SpeechOnnxKokoro` (`DemaConsulting.Speech.Onnx.Kokoro`) supplies a Kokoro-82M text-to-speech
  model and engine in two subsystems, `ModelManagementSubsystem` (catalog extension, model with its
  shared English text analyzer, phoneme vocabulary) and `SynthesisSubsystem` (the engine and the
  `KokoroPhonemePipeline`). It depends on `Speech`, `SpeechOnnx`, `SpeechTextEn` and `SpeechPhonemesEn`. The pipeline
  turns the renderer's weak forms on (the renderer's own default is off), because Kokoro was trained on phonemes with
  reduced function words.
- `SpeechOnnxPiper` (`DemaConsulting.Speech.Onnx.Piper`) supplies the Piper (VITS) English (US) `libritts_r` voice
  with 904 speakers as a text-to-speech model and engine in two subsystems, `ModelManagementSubsystem` (catalog
  extension, model with its archive extractor, voice configuration reader) and `SynthesisSubsystem` (the engine and
  the `PiperPhonemePipeline`). It depends on `Speech`, `SpeechOnnx`, `SpeechTextEn` and `SpeechPhonemesEn`, and on the
  SharpCompress OTS package to extract the two voice files from the downloaded archive; it bundles no espeak-ng.
- `SpeechOnnxNemotronStt` (`DemaConsulting.Speech.Onnx.NemotronStt`) supplies a streaming
  Nemotron speech-recognition model and engine in two subsystems, `ModelManagementSubsystem`
  and `RecognitionSubsystem` (the engine, feature extractor, RNN-T decoder, and silence limiter).
  It depends on `Speech` and `SpeechOnnx`.
- `SpeechOnnxChatterboxTurbo` (`DemaConsulting.Speech.Onnx.ChatterboxTurbo`) supplies a
  voice-cloning text-to-speech model and engine in two subsystems, `ModelManagementSubsystem`
  (catalog extension, model and options, tokenizer, reference voice) and `SynthesisSubsystem`
  (engine, networks, token selector, conditioning, capability profile). It depends on `Speech`
  and Microsoft.ML.OnnxRuntime (OTS) only; unlike the other two it does not use `SpeechOnnx`.
- `SpeechTextEn` (`DemaConsulting.Speech.Text.En`, a preview) supplies English text analysis in three subsystems,
  `AnalysisSubsystem` (the `EnglishTextAnalyzer`, its options, the annotation vocabulary, the pronunciation-override
  dictionary, the part-of-speech adapter and the counts-only diagnostics reporter), `NormalizationSubsystem` (the
  alignment model, the text normalizer, the numeric expression recognizer, the number verbalizer, the abbreviation
  resolver, the acronym recognizer, the web address recognizer and the alphanumeric token recognizer, with two embedded
  lexicons) and `HeteronymSubsystem` (the reading inventory with
  its two embedded tables, the feature extractor, the sparse perceptron model and the classifier, which uses the trained
  model embedded in the package); the first uses the other two, never the reverse. It implements Speech's
  `ITextAnalyzer` seam rather than a model or backend seam, depends on `Speech` and on the `DemaConsulting.PartOfSpeech`
  OTS package, and uses no inference runtime. Three further subsystems belong to the system but not to the package, all
  in the non-shipped trainer tool. `TrainingDataSubsystem` (the strict file reader, the typed data loader, the manifest
  verifier, the span validator, the finding collector, the schema, license and leakage checks and the command entry) is
  the `check-data` command that verifies the training data in `data/text-en/`, uses only the .NET base class library and
  is not used by the three runtime subsystems. `TrainingSubsystem` (the corpus loader, the deterministic random
  generator and math, the integer averaged perceptron, the model selection, the training record and command, the
  evaluator, the evaluation record and the evaluate command) is the `train` and `evaluate` commands that train and
  measure the heteronym model; it references the library and reuses its internals through `InternalsVisibleTo`, and the
  library does not know it exists. `ExternalEvaluationSubsystem` (the pinned source cache, the row conversion, the two
  dataset adapters, the external evaluation record and the command) is the `evaluate-external` command that measures the
  model on pinned, evaluation-only external datasets; it uses the evaluator and corpus loader of the `TrainingSubsystem`
  and the data folder discovery of the `TrainingDataSubsystem`, and none of them uses it. `ClusteringSubsystem` (the
  body extraction, the corpus cache, the snapshot, the leakage audit, the reader, the clusterer, the table, the record
  and the command) is the `cluster` command that induces the word-cluster table; it uses the deterministic logarithm and
  the tokenizer version of the `TrainingSubsystem`, the strict reader, data loader and sentence keys of the
  `TrainingDataSubsystem` and the library's tokenizer and lookup hash, and none of them uses it.
- `SpeechPhonemesEn` (`DemaConsulting.Speech.Phonemes.En`, a preview) supplies English phonemization in four
  subsystems, one per step of the pipeline: `LexiconSubsystem` (the embedded CMU Pronouncing Dictionary, the phone and
  pronunciation types, the morphology rules and the pronunciation of every reading id), `LetterToSoundSubsystem` (the
  trained integer network for words no dictionary knows and the converter around it), `PhonemizerSubsystem` (the
  `EnglishPhonemizer`, the word tokenizer and the word pronouncer) and `RenderingSubsystem` (the Piper IPA and Kokoro
  symbol renderers with the weak-form table and the layout and splitting they share). It depends on `Speech` (for the
  annotation type) and `SpeechTextEn` (for the annotation vocabulary and the reading inventory), uses no inference
  runtime, and is used only by the Kokoro and Piper packages. The data it embeds, the non-shipped tools project that
  builds and measures it (`tools/DemaConsulting.Speech.Phonemes.En.Tools`) and its evaluation records are tool inputs
  and outputs rather than software items; see *Modeling Scope and Exclusions*.

The command-line tool and the demo application compose these packages without any code of their own for them: both
register every model package's catalog extension - `AddSherpaModels()`, `AddKokoroModels()`, `AddPiperModels()`,
`AddNemotronSttModels()` and `AddChatterboxTurboModels()` - with the library's `SpeechModelCatalog` (the CLI in its
catalog adapter, the demo in its application class), so the Piper voice is listed, downloaded and selected like every
other model.

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
    ├── RecognitionResultBuffer.cs                — Backpressure buffer (coalesces provisionals, keeps finals)
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
    ├── SegmentAnnotation.cs                      — Opaque offset/length/kind/value annotation (public)
    ├── AnnotationList.cs                         — Shared empty-list and copy/bounds helpers (internal)
    ├── TextAnalysisResult.cs                     — Annotations plus optional sentence breaks (public)
    ├── TextAnalysisConventions.cs                — Tag placeholder constant (public)
    ├── ITextAnalyzer.cs                          — Once-per-request text-analysis contract (public)
    ├── TextAnalysisMapper.cs                     — Analysis-text construction and offset re-basing (internal)
    ├── IModelCapabilityProfile.cs                — Layer 2 rendering strategy contract (public)
    ├── DefaultModelCapabilityProfile.cs          — Generically-correct default rendering strategy (public)
    ├── SentenceChunker.cs                        — Sentence/clause-sized chunking (public)
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
    ├── IAnnotatedSynthesisBackend.cs              — Optional backend seam that also receives annotations (public)
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
- `SentenceChunk` is declared in `SentenceChunker.cs`.
- `AnalyzedToken` and `AnalyzedNormalization` are declared in `EnglishTextAnalysis.cs`, and
  `TextEnAnnotationSelection` in `EnglishTextAnalyzerOptions.cs`; all are documented with the unit that
  owns them (SpeechTextEn).
- `PronunciationOverrideEntry`, `OverrideAction` and `PronunciationScheme` are declared in
  `PronunciationOverrideEntry.cs`, and the internal `OverrideSyntax` grammars in `OverrideSyntax.cs`; all are
  documented with the PronunciationOverrides unit.
- `HeteronymContext` and `FeatureHashing` (the context builder and hash functions of the heteronym feature set) are
  documented with the HeteronymFeatureExtractor unit, `SparsePerceptronModelWriter` and `ModelBinaryFormat` with the
  SparsePerceptronModel unit, and `HeteronymModelProvider` (with `ModelLoadStatus`) with the HeteronymClassifier
  unit; all are internal and live in files of their own.
- `NormalizationKind` and `CliticKind` (the public enumerations of the NormalizationSubsystem) are
  declared in `NormalizationAlignment.cs` and are documented with the NormalizationAlignment unit, as are
  the internal `TokenDirective`, `TokenDirectiveKind`, `PronunciationMark` and `SpelledGrammar` declared there.
- `NumericSpan` and `NumericMatch` are declared in `NumericExpressionRecognizer.cs`, and `SpokenWriter`
  in `NumberVerbalizer.cs`; all are internal and are documented with the unit that owns them.

Several small internal helper types are likewise documented inside the unit that owns them rather
than in a design document of their own: `AudioFileReadOptions` (in the AudioFileReader and
FlacDecoder designs), `FlacFrameDecoder`, `FlacBitReader`, and `FlacCrc` (in the FlacDecoder
design), and `NumericParameterBounds` (in the SpeechModelParameters design).

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
│   ├── ConsoleCancellation.cs                 — Shared Ctrl+C (`Console.CancelKeyPress`) wiring
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
│   │   ├── DownloadProgressReporter.cs        — Shared throttled download-progress reporter
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
    ├── ChatterboxTurboTagTreatment.cs                     — Tag treatment table entry (record)
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
├── PACKAGE-LICENSE.md                                     — Package license file (packaging file, not a software item)
├── THIRD-PARTY-NOTICES.md                                 — Notices for embedded data (packaging file, not a software item)
├── SpeechModelCatalogKokoroExtensions.cs                  — `AddKokoroModels()` registration
├── ModelManagementSubsystem/
│   ├── OnnxKokoroEnglishSynthesisModel.cs                 — Kokoro multi-voice synthesis model
│   └── KokoroPhonemeVocabulary.cs                         — Phoneme-to-token-id vocabulary
├── Resources/
│   └── kokoro-v1.0-tokenizer.json                         — Embedded Kokoro phoneme vocabulary (Apache-2.0 data)
└── SynthesisSubsystem/
    ├── OnnxKokoroSynthesisEngine.cs                       — Real ONNX Runtime synthesis backend
    └── KokoroPhonemePipeline.cs                           — Text and annotations to phoneme token ids, in parts
```

The SpeechOnnxPiper library's folder structure likewise mirrors its software structure:

```text
src/DemaConsulting.Speech.Onnx.Piper/
├── PACKAGE-LICENSE.md                                     — Package license file (packaging file, not a software item)
├── THIRD-PARTY-NOTICES.md                                 — Voice license, attribution and notices (packaging file, not a software item)
├── SpeechModelCatalogPiperExtensions.cs                   — `AddPiperModels()` registration
├── ModelManagementSubsystem/
│   ├── OnnxPiperLibriTtsEnSynthesisModel.cs               — Piper libritts_r English (US) voice model
│   ├── PiperArchiveExtractor.cs                           — Extracts only the voice and its configuration from the archive
│   └── PiperVoiceConfig.cs                                — Voice configuration reader (symbol table, sample rate, scales)
└── SynthesisSubsystem/
    ├── OnnxPiperSynthesisEngine.cs                        — Real ONNX Runtime synthesis backend
    └── PiperPhonemePipeline.cs                            — Text and annotations to framed phoneme ids, in parts
```

The SpeechPhonemesEn library's folder structure likewise mirrors its software structure:

```text
src/DemaConsulting.Speech.Phonemes.En/
├── PACKAGE-LICENSE.md                                     — Package license file, MIT with the CMUdict notice (packaging file, not a software item)
├── THIRD-PARTY-NOTICES.md                                 — Notices for the embedded data (packaging file, not a software item)
├── Resources/
│   ├── cmudict-en.bin.gz                                  — Embedded CMU Pronouncing Dictionary image (data, built and verified by the tools project)
│   └── lts-en.bin.gz                                      — Embedded trained letter-to-sound network image (data, trained and verified by the tools project)
├── LexiconSubsystem/
│   ├── CmuLexicon.cs                                      — Internal dictionary lookup over the embedded image (with LexiconEntry)
│   ├── LexiconImageFormat.cs                              — Internal binary layout of the dictionary image
│   ├── Phone.cs                                           — Internal ARPAbet phone value type
│   ├── Pronunciation.cs                                   — Internal immutable sequence of phones
│   ├── LetterNames.cs                                     — Internal letter-name pronunciations
│   ├── Morphology.cs                                      — Internal stem-derived and clitic pronunciations (with SuffixKind and Derivation)
│   ├── ReadingPronunciations.cs                           — Internal pronunciation of every reading id (with ReadingEntry)
│   ├── LexiconCorrections.cs                              — Internal reviewed corrections of wrong dictionary entries and additions of missing words
│   └── NamespaceDoc.cs                                    — Namespace documentation (not a software item)
├── LetterToSoundSubsystem/
│   ├── LetterToSound.cs                                   — Internal caching converter that never leaves a word silent
│   ├── LtsModel.cs                                        — Internal letter-to-sound model: image reader, features and conversion
│   ├── LtsNetwork.cs                                      — Internal integer-only network
│   └── NamespaceDoc.cs                                    — Namespace documentation (not a software item)
├── PhonemizerSubsystem/
│   ├── EnglishPhonemizer.cs                               — Public text-to-phonemes entry point
│   ├── PhonemizedText.cs                                  — Public result: items, out-of-vocabulary words and unsupported tokens
│   ├── PhonemizedItem.cs                                  — Public result item (with PhonemizedItemKind and PronunciationSource)
│   ├── WordTokenizer.cs                                   — Internal tokenizer (with Token and TokenKind)
│   ├── SpokenText.cs                                      — Internal replacement application and origin map
│   ├── AnnotationIndex.cs                                 — Internal annotation lookup by span
│   ├── WordPronouncer.cs                                  — Internal priority list for the pronunciation of one word
│   └── NamespaceDoc.cs                                    — Namespace documentation (not a software item)
└── RenderingSubsystem/
    ├── EspeakIpaRenderer.cs                               — Public Piper IPA renderer (with EspeakIpaProfile)
    ├── EspeakWordRules.cs                                 — Internal Piper per-word sound rules
    ├── MisakiRenderer.cs                                  — Public Kokoro symbol renderer (with MisakiProfile)
    ├── MisakiWordRules.cs                                 — Internal Kokoro per-word sound rules
    ├── WeakForms.cs                                       — Internal function-word reduction table
    ├── PhonemeStream.cs                                   — Internal layout and long-text splitting shared by both renderers
    ├── WordShape.cs                                       — Internal word-shape helper of the rules
    └── NamespaceDoc.cs                                    — Namespace documentation (not a software item)
```

The Phonemes.En tools project is a non-shipped tool whose data and evidence are inputs and outputs of the library, not
software items:

```text
tools/DemaConsulting.Speech.Phonemes.En.Tools/            — Console tool: build-lexicon, train-lts, propose-readings, benchmark-lts, measure-morphology, measure-oracle and regenerate-goldens, with a README of their use
data/phonemes-en/                                         — Provenance README, MANIFEST.tsv pins, reading tables, renderer corpora (CMUdict itself is fetched into a git-ignored folder)
docs/evaluation/phonemes-en/                              — Recorded benchmark and measurement results: OOV model, renderers, Kokoro adoption, summary
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

The SpeechTextEn library's folder structure likewise mirrors its software structure:

```text
src/DemaConsulting.Speech.Text.En/
├── PACKAGE-LICENSE.md                                     — Package license file, MIT AND CC-BY-3.0-US (packaging file, not a software item)
├── THIRD-PARTY-NOTICES.md                                 — Attribution of the model's MASC-derived training data and notices for the dependency's embedded data (packaging file, not a software item)
├── Resources/
│   ├── abbreviations.tsv                                  — Embedded abbreviation lexicon (data, reviewed with AbbreviationResolver)
│   ├── acronyms.tsv                                       — Embedded initialism lexicon (data, reviewed with AcronymRecognizer)
│   └── heteronyms.bin                                     — Embedded trained heteronym model written by the trainer's train command (binary; pinned by training.json, not reviewed as text)
├── AnalysisSubsystem/
│   ├── EnglishTextAnalyzer.cs                             — English ITextAnalyzer: part of speech, normalization, readings and sentence breaks
│   ├── EnglishTextAnalysis.cs                             — Detailed, local-only analysis result with its token and normalization records
│   ├── EnglishTextAnalyzerOptions.cs                      — Options record and the annotation-selection flags enum
│   ├── TextEnAnnotations.cs                               — Annotation kind constants and typed readers
│   ├── PronunciationOverrides.cs                          — Immutable dictionary of host pronunciation overrides, TSV loader and token lookup
│   ├── PronunciationOverrideEntry.cs                      — Override entry record, its factories and the OverrideAction/PronunciationScheme enums
│   ├── OverrideSyntax.cs                                  — Internal key, payload and pronunciation grammars shared by the loader and the readers
│   ├── PartOfSpeechAdapter.cs                             — Internal seam over the DemaConsulting.PartOfSpeech package (with the sentence-to-context-token description shared with the trainer)
│   ├── AnalysisDiagnostics.cs                             — Internal counts-only diagnostics reporter
│   └── NamespaceDoc.cs                                    — Namespace documentation (not a software item)
├── HeteronymSubsystem/
│   ├── ReadingInventory.cs                                — Public inventory of heteronym readings and forms, with the internal reading id grammar
│   ├── HeteronymFeatureExtractor.cs                       — Internal deterministic feature extraction for one heteronym occurrence
│   ├── HeteronymContext.cs                                — Internal context token builder (word class, shape and tag codes)
│   ├── FeatureHashing.cs                                  — Internal stable hash functions, salts and the feature bucket of the feature set
│   ├── SparsePerceptronModel.cs                           — Internal sparse averaged-perceptron model and its strict loader
│   ├── SparsePerceptronModelWriter.cs                     — Internal model writer used by tests and the trainer
│   ├── ModelBinaryFormat.cs                               — Internal model file layout, checksum and compression primitives
│   ├── HeteronymClassifier.cs                             — Internal candidate-restricted classifier with calibrated confidence and the shared candidate scoring
│   ├── HeteronymModelProvider.cs                          — Internal lazy, once-only model provider and its load status
│   └── NamespaceDoc.cs                                    — Namespace documentation (not a software item)
└── NormalizationSubsystem/
    ├── NormalizationAlignment.cs                          — Alignment model, contract-enforcing builder and the public NormalizationKind/CliticKind enums
    ├── TextNormalizer.cs                                  — Internal rule-ordered normalization pass over the tokens
    ├── NumericExpressionRecognizer.cs                     — Internal token-local recognizer of numeric expressions (with the NumericSpan and NumericMatch values)
    ├── NumberVerbalizer.cs                                — Internal builder of the English words of a number (with the SpokenWriter helper)
    ├── AbbreviationResolver.cs                            — Internal abbreviation lexicon and neighbor-based reading choice
    ├── AcronymRecognizer.cs                               — Internal case-sensitive initialism lexicon and rules
    ├── WebAddressRecognizer.cs                            — Internal recognizer of URLs, e-mail addresses and bare domains
    ├── AlphanumericTokenRecognizer.cs                     — Internal recognizer of letter-digit identifiers such as A4 and MP3
    └── NamespaceDoc.cs                                    — Namespace documentation (not a software item)
```

The Text.En trainer is a non-shipped tool whose source and tests are reviewed like the libraries'. Its four
subsystems have the following layout, and the data it reads is a tool input in a separate folder, not a software item:

```text
tools/DemaConsulting.Speech.Text.En.Trainer/
├── Program.cs                                             — Entry point glue that dispatches to the five commands (not a unit)
├── README.md                                              — Usage of the tool (not a software item)
├── RUN-LOG.md                                             — Hand-written record of the model configuration, the gate runs and the external evaluation run (not a software item)
├── training.json                                          — Committed training record of the shipped model: settings, input hashes and the model image hash (written by train, reviewed data)
├── external-sources.json                                  — Pins of the external evaluation datasets: revision, archive address, license, file count, size and listing hash (reviewed data)
├── external-files.tsv                                     — Per-file pins of the external evaluation datasets: path, SHA-256 and size (reviewed data)
├── TrainingDataSubsystem/
│   ├── CheckDataCommand.cs                                — Command line entry: options, folder discovery, check order, exit codes
│   ├── CheckReport.cs                                     — Collector of findings and information lines; decides the exit code
│   ├── TsvTable.cs                                        — Strict tab-separated file reader (with the TsvRow value)
│   ├── TextEnDataSet.cs                                   — Typed loader of every table, with the source vocabulary and its records
│   ├── DataManifest.cs                                    — Verifier of MANIFEST.tsv against the data folder
│   ├── SpanValidator.cs                                   — Strict whole-word span check
│   ├── SchemaChecker.cs                                   — Inventory, reference, row, provenance-id and gap-table checks (with ReadingCounts)
│   ├── LicenseChecker.cs                                  — License allow-list, source policy, restricted terms, paths, attribution
│   ├── LeakageChecker.cs                                  — Split separation and optional external evaluation-set check (with SentenceKeys)
│   ├── EvaluationOverlayChecker.cs                        — Regression overlay and generalization set checks: references, seen marks, novelty, quotas (with GeneralizationShape)
│   └── ClusterDataChecker.cs                              — Offline word-cluster checks: table schemas, corpus leakage, record hashes and tokenizer version
├── TrainingSubsystem/
│   ├── TrainingCorpus.cs                                  — Loader of the four training inputs; row preparation with the runtime's own units (with LabeledRow, PreparedRow and RowPreparer)
│   ├── DeterministicRandom.cs                             — SplitMix64 generator with unbiased draws and a Fisher-Yates shuffle
│   ├── DeterministicMath.cs                               — Exponential and natural logarithm from basic arithmetic only
│   ├── AveragedPerceptronTrainer.cs                       — Candidate-restricted integer averaged perceptron (with EpochWeights and ShortWeights)
│   ├── ModelSelection.cs                                  — Development-set epoch and temperature choices and the model images (with TrainedModel and EpochScore)
│   ├── TrainingRecord.cs                                  — Training and evaluation settings, inputs, result and the training.json form (with TrainingSettings, EvaluationSettings, TrainingInputs and TrainingResult)
│   ├── TrainCommand.cs                                    — Command line entry of train and train --check
│   ├── HeteronymEvaluator.cs                              — End-to-end evaluation, baselines, strata, clustered bootstrap, gate, parity and the overlay evaluation with and without the cue priors (with its result records)
│   ├── EvaluationOverlay.cs                               — Loader of the regression overlay and the generalization set with strata, slices and clusters (with OverlayRow)
│   ├── EvaluationRecord.cs                                — Eight deterministic tab-separated evaluation record files (with EvaluationReport)
│   └── EvaluateCommand.cs                                 — Command line entry of evaluate
└── ExternalEvaluationSubsystem/
    ├── ExternalSourceCache.cs                             — Pins, pinned fetch into the cache and verified reading of cached files (with PinnedFile, ExternalSourcePin, ExternalSourcePins and the default download)
    ├── ExternalRowConversion.cs                           — Byte offsets, label mapping and row conversion with skip accounting (with ExternalRecord, ExternalReadResult, ConversionCounts, ExternalRowSet, SkipReasons, ByteOffsets, ReadingIdMap and ExternalRowConverter)
    ├── WikipediaHomographAdapter.cs                       — Reader of the pinned WikipediaHomographData files
    ├── HeteronymBenchAdapter.cs                           — Reader of the pinned English read set of heteronym-bench
    ├── ExternalEvaluationRecord.cs                        — Seven deterministic tab-separated external record files (with ExternalEvaluationReport, ExternalSetResult and ExternalSeedResult)
    └── EvaluateExternalCommand.cs                         — Command line entry of evaluate-external
└── ClusteringSubsystem/
    ├── GutenbergBody.cs                                   — Body extraction and normalization of a downloaded e-book (with GutenbergBodyText)
    ├── ClusterCorpusCache.cs                              — Source list, mirror-only rate-limited fetch, body pins and verified bodies (with ClusterSource, ClusterSourceList, CorpusFetchResult and GutenbergMirrorFetcher)
    ├── CorpusSnapshot.cs                                  — Byte-reproducible ustar snapshot of the corpus (with CorpusSnapshotInfo)
    ├── CorpusLeakageAudit.cs                              — Exact, near and contains audit against every evaluation set and the removal list (with EvaluationSentence, CorpusRemoval and AuditCount)
    ├── ClusterCorpusReader.cs                             — Tokenization with the analyzer's tokenizer, runtime-key self-check and integer counts (with CorpusParagraph, CorpusBook, TokenizedCorpus, SkippedSentence, ReaderSettings and CorpusCounts)
    ├── ClusterInducer.cs                                  — Deterministic windowed mutual-information clustering (with BigramGraph, ClusterMerge, InducedClusters, InducerOptions, InducerProgress and InducerSnapshot)
    ├── WordClusterTable.cs                                — Table rows, the shared-hash drop rule and the word-clusters.tsv text form (with WordClusterEntry)
    ├── ClusteringRecord.cs                                — The clustering.json record (with ClusteringSettings, ClusteringInputs, ClusteringCorpus and ClusteringResult)
    └── ClusterCommand.cs                                  — Command line entry of cluster (with ClusterEnvironment)

data/text-en/
├── README.md, ATTRIBUTION.md, MANIFEST.tsv                — Documents, attribution and the pinned manifest (reviewed data)
├── readings.tsv, forms.tsv                                — Reading inventory and forms (both also embedded in the package, read by ReadingInventory) (reviewed data)
├── whd-mapping.tsv, bench-mapping.tsv                     — Id-only evaluation mappings of the external datasets' labels to reading identifiers (reviewed data)
├── extension-decisions.tsv                                — Decisions on candidate readings (reviewed data)
├── train.tsv, dev.tsv, holdout-natural.tsv, holdout-independent.tsv — Labeled rows (large; pinned by the manifest)
├── harvest-provenance.tsv, synthetic-provenance.tsv, gaps-v4.tsv, leakage-report.tsv — Records and reports
├── regression.tsv, generalization.tsv                     — Evaluation overlays: references to flagged, golden and existing sentences, and the blind generalization set (reviewed data)
├── cluster-sources.tsv, cluster-removals.tsv, clustering.json — Word-cluster corpus pins and license record, audit removals and the record of the table (reviewed data)
├── word-clusters.tsv                                      — Word-cluster table (large; pinned by the manifest and clustering.json)
├── exclusions.tsv, extension-candidates.tsv, check-output.txt — Removed rows, candidates and the recorded check output
├── audit/                                                 — Blind audit records and their summary
├── prompts/                                               — The prompts that produced the generated and labeled data
└── external/                                              — Git-ignored caches: the external evaluation datasets (evaluate-external --fetch) and the word-cluster corpus in cluster-corpus/ (cluster --fetch) (never committed)

docs/evaluation/text-en/
├── README.md                                              — Method, headline results and the limits of the evidence (hand-written, reviewed data)
├── summary.tsv, gate.tsv, by-source.tsv, per-reading.tsv, confidence.tsv, seeds.tsv, overlay-rows.tsv, curated-ablation.tsv — Deterministic evaluation record written by evaluate --write and verified by evaluate --check (reviewed data)
└── external/
    ├── README.md                                          — Method, results and the limits of the external evidence (hand-written, reviewed data)
    └── sources.tsv, conversion.tsv, summary.tsv, by-source.tsv, per-reading.tsv, confidence.tsv, seeds.tsv — Deterministic external evaluation record written by evaluate-external --write and verified by evaluate-external --check (reviewed data)
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
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#); the Text.En trainer's source is the exception
  in `tools/DemaConsulting.Speech.Text.En.Trainer/.../{Item}.cs`
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#); the trainer's tests are in
  `test/DemaConsulting.Speech.Text.En.Trainer.Tests/{Subsystem}/{Item}Tests.cs`, with the subsystem-level tests
  directly in that folder (`TrainingDataSubsystemTests.cs`, `TrainingSubsystemTests.cs`,
  `ExternalEvaluationSubsystemTests.cs` and `ClusteringSubsystemTests.cs`, with the shared fixtures in `Fakes/`)
- SysML2 model: `docs/sysml2/model/{system}/.../{item}.sysml` (kebab-case)
- Review-sets: defined in `.reviewmark.yaml`

Each sibling system has its own parallel `{system}` folders in these trees - for example,
SpeechSherpa's artifacts live under `docs/reqstream/speech-sherpa/`, `docs/design/speech-sherpa/`,
`docs/verification/speech-sherpa/`, `src/DemaConsulting.Speech.Sherpa/`, and
`test/DemaConsulting.Speech.Sherpa.Tests/`. SpeechSherpa's subsystems share their names with the
Speech subsystems they extend, so each subsystem is always identified together with its system.

## Modeling Scope and Exclusions

The SysML2 model intentionally follows these rules; they are not omissions:

- **System-to-system dependencies are prose-only.** The dependencies between sibling systems
  (for example SpeechSherpa, the ONNX model packages, SpeechCli and SpeechDemo all building on
  Speech, or the ONNX model packages building on SpeechOnnx) are documented in each system's
  design document and are enforced by the project files; the model's `part` ownership expresses
  only decomposition (system, subsystem, unit), not inter-system dependency.
- **OTS parts are modeled only where integration code uses the item.** SpeechCli and SpeechDemo
  reference `Microsoft.ML.OnnxRuntime` directly only to pin its managed API to the ONNX Runtime release that
  sherpa-onnx's native library bundles (with the native assets excluded, so sherpa's own native
  library is the one deployed); they contain no code that calls it, so they
  carry no OnnxRuntime OTS part usage. The OTS item itself is modeled and verified under SpeechOnnx. Build-tool OTS
  items are repository-level tooling (see the note in `ots.sysml`).
- **Non-unit source files are not modeled.** `NamespaceDoc.cs` files (documentation-only
  namespace holders) and Avalonia `*.axaml.cs` view code-behind files contain no independent
  behavior and are covered by the view-model or panel unit they accompany. The trainer's `Program.cs`
  is entry-point glue that only dispatches to `CheckDataCommand`, `TrainCommand`, `EvaluateCommand`,
  `EvaluateExternalCommand` and `ClusterCommand`; it is listed in
  the `sourceRef` of those units.
- **Data is a tool input, not a software item.** The training and evaluation data in `data/text-en/` is described
  in the design of the subsystem that checks it (*SpeechTextEn TrainingDataSubsystem Design*), reviewed through its
  own review sets and pinned by its manifest; it has no requirement, design or SysML2 part of its own. Likewise the
  trained model `Resources/heteronyms.bin` is data produced by the training subsystem: it is pinned by
  `training.json` and verified by `train --check`, and the committed evaluation record in `docs/evaluation/text-en/`
  is verified by `evaluate --check`; neither has a software item of its own. The external evaluation follows the same
  rule: the pins `external-sources.json` and `external-files.tsv`, the mapping `data/text-en/bench-mapping.tsv` and the
  external record in `docs/evaluation/text-en/external/` (verified by `evaluate-external --check`) are tool inputs and
  outputs described by the design of *SpeechTextEn ExternalEvaluationSubsystem Design*; the external datasets themselves
  are never committed and are not a software item, an OTS item or a shared package of this repository. The word-cluster
  corpus follows the same rule: the source list `cluster-sources.tsv`, the removal list `cluster-removals.tsv`, the
  table `word-clusters.tsv` and its record `clustering.json` (verified offline by `check-data` against the committed
  files and the tokenizer, and fully by the manual `cluster --check`) are tool inputs and
  outputs described by *SpeechTextEn ClusteringSubsystem Design*, and the books themselves are only cached, never
  committed.
- **SpeechPhonemesEn data is a tool input, not a software item either.** The embedded dictionary and letter-to-sound
  images (`Resources/cmudict-en.bin.gz`, `Resources/lts-en.bin.gz`) are produced and verified by the non-shipped
  `DemaConsulting.Speech.Phonemes.En.Tools` project (`build-lexicon --check`, `train-lts --check`, `propose-readings
  --check`) from the pinned CMU Pronouncing Dictionary and the reading tables in `data/phonemes-en/`, whose pins are in
  `data/phonemes-en/MANIFEST.tsv`; the measurements are recorded in `docs/evaluation/phonemes-en/`. They are described
  by *SpeechPhonemesEn Design* and its subsystems (the data provenance section), have no software item of their own,
  and the CMU Pronouncing Dictionary is data, not an OTS item.
- **Small enums and value types** (for example `TaggedTextSpanKind` and the
  ChatterboxTurbo variant, policy, tag and generation-result types) are listed in the `sourceRef`
  of the unit that owns or consumes them rather than modeled as separate units.
- **View names** are prefixed with the system name for sibling systems; the core Speech
  system's views are unprefixed, and `DiagnosticsView`/`PortAudioView` name child subsystems
  that follow their own short names, as explained in `design-views.sysml`.

## References

- Speech User Guide — the compiled User Guide document for this repository.
- Speech Repository — the Speech source repository hosted on GitHub.
