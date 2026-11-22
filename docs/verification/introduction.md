# Introduction

This document provides the verification design for the Speech library, a cross-platform
.NET library providing local, offline speech-to-text and text-to-speech services for desktop
applications.

## Purpose

The purpose of this document is to serve as the verification design entry point and document how
requirements will be tested across all software items in the Speech system. This documentation
enables formal review by describing named test scenarios for each software item (the ReqStream
trace matrix tracks which requirements each test covers), supports compliance
auditing by providing clear traceability from requirements through verification design to tests,
and ensures test completeness can be assessed without reading implementation code.

This document is intended for:

- Software developers implementing and maintaining tests
- Code reviewers validating test completeness against requirements
- Compliance auditors tracing requirements through verification design to tests
- Quality assurance teams validating test coverage and scenario adequacy

## Scope

This document covers the verification design for the Speech system and its constituent software
items, for the SpeechSherpa library system, for the SpeechOnnx, SpeechOnnxKokoro, SpeechOnnxPiper,
SpeechOnnxNemotronStt, and SpeechOnnxChatterboxTurbo library systems, for the SpeechTextEn and
SpeechPhonemesEn library systems, for the SpeechCli command-line tool system, and for the SpeechDemo application
system, each with its constituent software items, specifically:

- **Speech (System)** — The complete .NET library system providing speech capture, recognition,
  and synthesis capabilities to host applications
- **Diagnostics (Subsystem)** — Reports structural diagnostic events to an optional
  host-supplied sink
- **AudioSubsystem (Subsystem)** — Audio contracts, selection logic, honest fallbacks, real
  PortAudio-backed device/probe implementations, file-backed WAV devices, and managed WAV/FLAC
  decoding and resampling
- **PortAudio (Subsystem)** — Internal child subsystem under AudioSubsystem that isolates
  PortAudioSharp2 and supplementary host-API P/Invoke bindings behind mockable abstractions
- **RecognitionSubsystem (Subsystem)** — Streaming speech-to-text: recognizer contract and
  result types, composition with honest unavailable fallback, the capture-to-engine pipeline and
  its audio-format converter, and the public, mockable recognition-backend seam
- **ModelManagementSubsystem (Subsystem)** — Per-user speech model storage, download/install
  orchestration with an optional mirror, reference-voice packs and library, and the
  empty-by-default, host-populated model catalog
- **SynthesisSubsystem (Subsystem)** — Text-to-speech: the closed Natural Language Audio Tag
  vocabulary, the async Engine/Session synthesis API, and the public, mockable synthesis-backend seam

The following software items of the SpeechSherpa system are also covered:

- **SpeechSherpa (System)** — The sibling .NET library that supplies the shipped sherpa-onnx-backed
  recognition and synthesis models and their native backends through the Speech library's
  `IRecognitionModel`/`ISynthesisModel` extension seam; `Speech` must never depend on it
- **ModelManagementSubsystem (Subsystem)** — The four shipped sherpa-onnx models, their archive
  extraction and transcript-restoration helpers, and the `AddSherpaModels` catalog registration
  extension
- **RecognitionSubsystem (Subsystem)** — The real, native-backed `SherpaOnnxRecognitionEngine`
  recognition backend
- **SynthesisSubsystem (Subsystem)** — The real, native-backed `SherpaOnnxSynthesisEngine`
  synthesis backend

The following software items of the SpeechOnnx family of systems are also covered; each is a
sibling system that `Speech` must never depend on:

- **SpeechOnnx (System)** and its **OnnxRuntimeSubsystem (Subsystem)** — Shared ONNX Runtime
  infrastructure: the layered execution-provider selector with CPU fallback
- **SpeechOnnxKokoro (System)** — The ONNX Runtime Kokoro-82M text-to-speech package, with its
  **ModelManagementSubsystem** (catalog extension, model with its shared English text analyzer,
  phoneme vocabulary) and **SynthesisSubsystem** (the engine and the phoneme pipeline that turns
  text and annotations into token ids)
- **SpeechOnnxPiper (System)** — The ONNX Runtime Piper (VITS) English (US) `libritts_r` text-to-speech
  package, with its **ModelManagementSubsystem** (catalog extension, voice model with its archive
  extractor, voice configuration reader) and **SynthesisSubsystem** (the engine and the phoneme
  pipeline that turns text and annotations into framed phoneme ids)
- **SpeechOnnxNemotronStt (System)** — The ONNX Runtime Nemotron streaming speech-recognition
  package, with its **ModelManagementSubsystem** (catalog extension, model, vocabulary) and
  **RecognitionSubsystem** (the engine, feature extractor, RNN-T decoder, silence limiter)
- **SpeechOnnxChatterboxTurbo (System)** — The ONNX Runtime voice-cloning text-to-speech package,
  with its **ModelManagementSubsystem** (catalog extension, model and options, tokenizer,
  reference voice) and **SynthesisSubsystem** (engine, networks, token selector, capability
  profile)

The following software items of the SpeechTextEn system are also covered; it is a sibling system that
`Speech` must never depend on:

- **SpeechTextEn (System)** — The `DemaConsulting.Speech.Text.En` English text-analysis package
  (a preview), verified through its public API and end to end through the real Speech synthesis
  session, with its **AnalysisSubsystem** (the analyzer, its options, the annotation vocabulary,
  the part-of-speech adapter and the diagnostics reporter) and its **NormalizationSubsystem** (the
  alignment model, the text normalizer, the numeric expression recognizer, the number verbalizer, the
  abbreviation resolver, the acronym recognizer, the web address recognizer and the alphanumeric token
  recognizer, whose
  tests feed the real tokenizer's tokens to the normalizer through a test helper), and the non-shipped subsystems of
  the Text.En trainer, all verified in `test/DemaConsulting.Speech.Text.En.Trainer.Tests` over data folders, models,
  datasets and books that the tests build in code (and once over the real committed data): the
  **TrainingDataSubsystem** (the `check-data` command that verifies the training data in `data/text-en/`: the strict
  file reader, the typed loader, the manifest verifier, the span validator, the finding collector, the schema,
  license, leakage, evaluation-overlay and corpus checks and the command entry), the **TrainingSubsystem** (the
  `train` and `evaluate` commands, including the overlay evaluation with and without the cue priors), the
  **ExternalEvaluationSubsystem** (the `evaluate-external` command, offline on fake datasets) and the
  **CorpusSubsystem** (the `corpus` command that fetches and audits the public-domain corpus, offline on
  generated books served by a fake mirror)
- **SpeechPhonemesEn (System)** — The `DemaConsulting.Speech.Phonemes.En` English phonemizer package (a preview),
  verified through its public API with golden corpora (sentences, readings, out-of-vocabulary words and both
  renderers) and symbol-coverage gates against the Piper symbol table and the Kokoro vocabulary, with its
  **LexiconSubsystem** (the embedded dictionary, the phone types, morphology and reading pronunciations),
  **LetterToSoundSubsystem** (the trained network and its converter), **PhonemizerSubsystem** (the phonemizer, the
  word tokenizer and the word pronouncer) and **RenderingSubsystem** (the Piper IPA and Kokoro symbol renderers, the
  weak-form table and the shared layout and splitting)

The following software items of the SpeechCli system are also covered:

- **SpeechCli (System)** — The cross-platform `speech-cli` .NET global tool, with its five
  subsystems: **ModelCommandsSubsystem** (the seven model-management subcommands),
  **DeviceCommandsSubsystem** (three device subcommands), **SynthesisCommandSubsystem** (`speak`),
  **RecognitionCommandSubsystem** (`recognize`), and **ConversationCommandSubsystem** (`ask`)

The following software items of the SpeechDemo system are also covered:

- **SpeechDemo (System)** — The Avalonia desktop application that demonstrates the Speech
  library using only its public API, verified by composing the application exactly as its own
  composition root does
- **ShellSubsystem (Subsystem)** — The manual composition root and capability-navigation window
  shell
- **DeviceSelectionSubsystem (Subsystem)** — The capture/playback device pickers and the
  demo-owned enumeration seam over the library's device probes
- **ModelCatalogSubsystem (Subsystem)** — The model catalog and download panel and the
  demo-owned catalog seam over the library's model catalog
- **ModelSettingsSubsystem (Subsystem)** — The generic per-model parameter settings panel
- **SynthesisPanelSubsystem (Subsystem)** — The text-to-speech panel and its demo-owned
  synthesizer-session seam
- **RecognitionPanelSubsystem (Subsystem)** — The speech-to-text panel, its demo-owned
  recognizer-session seam, and the opt-in capture-to-WAV diagnostic recorder
- **App, AppLaunchOptions, MirrorOptionsFactory, Program (Units)** — The units that belong
  directly to the SpeechDemo system: the composition root, the command-line options, the shared
  mirror-option validation, and the process entry point

The following OTS items are also covered:

- **PortAudioSharp2** — managed PortAudio binding and transitive native runtime carrier
- **SherpaOnnx** — managed local speech-inference API used by SpeechSherpa
- **SharpCompress** — managed `.tar.bz2` archive reader used by SpeechSherpa model installs
- **System.Numerics.Tensors** — vectorized `TensorPrimitives` used by the Speech audio pipeline
- **Microsoft.ML.OnnxRuntime** — managed ONNX Runtime API used by the SpeechOnnx libraries
- **Avalonia** — cross-platform desktop UI framework hosting the SpeechDemo application
- **CommunityToolkit.Mvvm** — MVVM change-notification and command source generators
- **DemaConsulting.PartOfSpeech** — English tokenizer and part-of-speech tagger used by SpeechTextEn
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

This verification documentation covers the same software items as the design documentation.

Version applicability: This verification design applies to all versions of the Speech.

The following topics are explicitly excluded from this verification documentation:

- Build pipeline and CI/CD process testing
- Infrastructure and hosting environment testing
- Automated proof of true end-to-end microphone/speaker hardware I/O in CI
- Automated proof of real speech-recognition accuracy through a downloaded model and the native
  speech-inference runtime in CI
- Automated user-interface (window, layout, and interaction) testing of the SpeechDemo
  application in CI
- Automated proof of a real, multi-hundred-megabyte model download by the SpeechDemo application
  or the SpeechSherpa models

## Companion Artifact Structure

Each software item covered by this document has corresponding artifacts in parallel directory
trees. In-house items have artifacts in these parallel locations:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

The SpeechSherpa system follows the same pattern as a sibling system: its verification design
lives under `docs/verification/speech-sherpa/` (system document `docs/verification/speech-sherpa.md`),
its design under `docs/design/speech-sherpa/`, its source under `src/DemaConsulting.Speech.Sherpa/`,
and its tests under `test/DemaConsulting.Speech.Sherpa.Tests/`. The SpeechCli, SpeechDemo, SpeechOnnx,
SpeechOnnxKokoro, SpeechOnnxPiper, SpeechOnnxNemotronStt, SpeechOnnxChatterboxTurbo, SpeechTextEn, and
SpeechPhonemesEn systems follow the
same sibling-system pattern under their own kebab-case folders and `test/DemaConsulting.Speech.*.Tests`; the
trainer tool of SpeechTextEn keeps its source in `tools/DemaConsulting.Speech.Text.En.Trainer/` and its tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/`, and the tools project of SpeechPhonemesEn
(`tools/DemaConsulting.Speech.Phonemes.En.Tools/`) is non-shipped evidence tooling without a test project of its own,
whose `--check` commands and the golden-corpus tests of SpeechPhonemesEn are its verification.

OTS items have parallel artifacts in:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml` (kebab-case)
- Verification: `docs/verification/ots/{ots-name}.md` (kebab-case)

Review-sets: defined in `.reviewmark.yaml`

## References

- Speech User Guide — the compiled User Guide document for this repository.
- Speech Repository — the Speech source repository hosted on GitHub.
