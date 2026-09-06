# OTS Integration Design

This document describes the overall Off-The-Shelf (OTS) integration strategy for the Speech.

## Overview

DemaConsulting.Speech now has three runtime NuGet dependencies: `PortAudioSharp2`,
`org.k2fsa.sherpa.onnx`, and `SharpCompress`. The first provides the managed PortAudio binding and
brings its supported native runtime packages transitively; the second provides the managed local
speech-inference API and likewise brings its per-platform native runtime packages transitively;
the third provides the managed BZip2-compressed tar (`.tar.bz2`) archive reader that
`TarBz2ArchiveExtractor` uses to unpack downloaded recognition-model archives after checksum
verification.
The library's own source code still depends primarily on the .NET Base Class Library, but real
audio I/O and real speech recognition now flow through these OTS dependencies.

The SpeechDemo application adds two runtime NuGet dependencies of its own, `Avalonia` and
`CommunityToolkit.Mvvm`, which supply its user interface and its MVVM plumbing respectively.
Neither is referenced by the Speech library, and the library's project file fails the build if an
`Avalonia*` package reference ever appears in it.

All remaining OTS items listed below are build-time and quality-pipeline tools, not runtime
library dependencies. Each OTS item provides one stage of the documentation,
requirements-traceability, testing, and quality-reporting pipeline invoked by `build.ps1`,
`lint.ps1`, and the CI workflow.

## OTS Items

| OTS Item | Purpose |
| --- | --- |
| PortAudioSharp2 | Managed PortAudio binding and carrier for supported native runtime packages |
| SherpaOnnx | Managed local speech-inference API and carrier for its native runtime packages |
| SharpCompress | Managed BZip2/tar archive reader for unpacking downloaded model archives |
| Avalonia | Cross-platform desktop UI framework hosting the SpeechDemo application |
| CommunityToolkit.Mvvm | MVVM change-notification and command source generators for SpeechDemo |
| BuildMark | Generates build-notes documentation from GitHub Actions metadata |
| FileAssert | Validates generated documents against acceptance criteria |
| Pandoc | Converts Markdown documentation to HTML |
| ReqStream | Enforces requirements-to-test traceability |
| ReviewMark | Enforces file review coverage and currency |
| SarifMark | Converts CodeQL SARIF results into a markdown report |
| SonarMark | Generates a SonarCloud quality report |
| SysML2Tools | Validates the SysML2 architecture model and renders its views to SVG |
| VersionMark | Captures and publishes tool-version information |
| WeasyPrint | Converts HTML documentation to PDF |
| xUnit | Discovers and executes unit and integration tests |

Each item's individual design document records its Purpose, Features Used, and Integration
Pattern. Each item's requirements and verification evidence are recorded in
`docs/reqstream/ots/{ots-name}.yaml` and `docs/verification/ots/{ots-name}.md` respectively.
