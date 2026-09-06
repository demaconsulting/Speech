# Introduction

This document provides the verification design for the Speech, a cross-platform
.NET library providing local, offline speech-to-text and text-to-speech services for desktop
applications.

## Purpose

The purpose of this document is to serve as the verification design entry point and document how
requirements will be tested across all software items in the Speech system. This documentation
enables formal review by mapping every requirement to named test scenarios, supports compliance
auditing by providing clear traceability from requirements through verification design to tests,
and ensures test completeness can be assessed without reading implementation code.

This document is intended for:

- Software developers implementing and maintaining tests
- Code reviewers validating test completeness against requirements
- Compliance auditors tracing requirements through verification design to tests
- Quality assurance teams validating test coverage and scenario adequacy

## Scope

This document covers the verification design for the Speech system and its constituent software
items, and for the SpeechDemo application system and its constituent software items,
specifically:

- **Speech (System)** — The complete .NET library system providing speech capture, recognition,
  and synthesis capabilities to host applications
- **Diagnostics (Subsystem)** — Reports structural diagnostic events to an optional
  host-supplied sink
- **AudioSubsystem (Subsystem)** — Audio contracts, selection logic, honest fallbacks, and real
  PortAudio-backed device/probe implementations
- **PortAudio (Subsystem)** — Internal child subsystem under AudioSubsystem that isolates
  PortAudioSharp2 and supplementary host-API P/Invoke bindings behind mockable abstractions
- **RecognitionSubsystem (Subsystem)** — Streaming speech-to-text: recognizer contract and
  result types, composition with honest unavailable fallback, the capture-to-engine pipeline and
  its audio-format converter, and the mockable recognition-engine seam

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
- Automated proof of a real model download by the SpeechDemo application, since the library
  deliberately ships no downloadable model in this phase

## Companion Artifact Structure

Each software item covered by this document has corresponding artifacts in parallel directory
trees. In-house items have artifacts in these parallel locations:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

OTS items have parallel artifacts in:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml` (kebab-case)
- Verification: `docs/verification/ots/{ots-name}.md` (kebab-case)

Review-sets: defined in `.reviewmark.yaml`

## References

- Speech User Guide — the compiled User Guide document for this repository.
- Speech Repository — the Speech source repository hosted on GitHub.
