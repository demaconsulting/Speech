# OTS Verification Evidence

This document describes the overall Off-The-Shelf (OTS) verification strategy for the Speech.

## Overview

Runtime OTS dependencies and build-time OTS tools are verified differently in this repository.
For build/pipeline tools, verification is a combination of self-validation CLI modes and
pipeline-evidence-based verification. For the runtime PortAudio and sherpa-onnx dependencies,
automated verification focuses on deterministic integration logic, while true end-to-end physical
audio I/O and real native speech inference remain manual/local verification only. For the
SpeechDemo application's Avalonia and CommunityToolkit.Mvvm dependencies, automated verification
covers application-host configuration and the generated MVVM members, while visual appearance and
interactive behavior remain manual/local verification only.

## OTS Items

| OTS Item | Verification Approach |
| --- | --- |
| PortAudioSharp2 | Deterministic integration tests for managed binding use; manual/local hardware I/O verification |
| SherpaOnnx | Deterministic integration tests for managed configuration use; manual native-inference verification |
| Avalonia | Deterministic application-host configuration tests; manual/local UI verification |
| CommunityToolkit.Mvvm | Deterministic tests over the generated properties and commands |
| BuildMark | Self-validation CLI suite plus pipeline evidence via build-notes document |
| FileAssert | Self-validation CLI suite plus transitive evidence from document assertions |
| Pandoc | Pipeline evidence: FileAssert assertions on each generated HTML document |
| ReqStream | Self-validation CLI suite plus pipeline evidence via enforce traceability |
| ReviewMark | Self-validation CLI suite plus pipeline evidence via review plan/report |
| SarifMark | Self-validation CLI suite plus pipeline evidence via SARIF markdown report |
| SonarMark | Self-validation CLI suite plus pipeline evidence via SonarCloud report |
| SysML2Tools | Self-validation CLI suite plus pipeline evidence via lint and rendered SVGs |
| VersionMark | Self-validation CLI suite plus pipeline evidence via version data |
| WeasyPrint | Pipeline evidence: FileAssert assertions on each generated PDF document |
| xUnit | Self-validation via discovery, execution, and TRX reporting of tests |
