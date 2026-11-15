# OTS Verification Evidence

This document describes the overall Off-The-Shelf (OTS) verification strategy for the Speech.

## Overview

Runtime OTS dependencies and build-time OTS tools are verified differently in this repository.
For build/pipeline tools, verification is a combination of self-validation CLI modes and
pipeline-evidence-based verification. For the Speech library's runtime PortAudio dependency, its
System.Numerics.Tensors dependency, the SpeechSherpa library's sherpa-onnx and SharpCompress
dependencies, and the SpeechOnnx libraries' Microsoft.ML.OnnxRuntime dependency, automated
verification focuses on deterministic integration logic, while true end-to-end physical audio I/O
and real native speech inference remain manual/local verification only. For the
SpeechDemo application's Avalonia and CommunityToolkit.Mvvm dependencies, automated verification
covers application-host configuration and the generated MVVM members, while visual appearance and
interactive behavior remain manual/local verification only.

Verification evidence follows the requirements structure. Each runtime OTS requirement is a
child of a requirement of the software item that uses it, so the tests that verify the OTS
requirement also support that parent. Each build/pipeline tool's requirements are children of
the repository quality requirements in `docs/reqstream/quality.yaml` (`Speech-Quality-*`), which
state repository outcomes without naming the tool and carry no tests of their own; the evidence
is the OTS requirements' own tests and pipeline output.

## OTS Items

| OTS Item | Verification Approach |
| --- | --- |
| PortAudioSharp2 | Deterministic integration tests for managed binding use; manual/local hardware I/O verification |
| SherpaOnnx | Deterministic integration tests for managed configuration use; manual native-inference verification |
| SharpCompress | Deterministic in-memory `.tar.bz2` fixture extraction tests |
| System.Numerics.Tensors | Deterministic tests over the audio math that calls `TensorPrimitives` |
| Microsoft.ML.OnnxRuntime | Deterministic tests over tiny in-memory graphs; manual real-model and GPU verification |
| Avalonia | Deterministic application-host configuration tests; manual/local UI verification |
| CommunityToolkit.Mvvm | Deterministic tests over the generated properties and commands |
| DemaConsulting.TestResults | Deterministic tests over the TRX and JUnit XML files written by SpeechCli self-validation |
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
