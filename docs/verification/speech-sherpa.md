# SpeechSherpa System Verification Design

This document describes the system-level verification strategy for the SpeechSherpa library.

## Verification Approach

SpeechSherpa is verified at system level through its one public entry point, the
`AddSherpaModels()` extension method, exercised against a real Speech `SpeechModelCatalog` exactly
as a host composes it: the tests prove the method returns the same catalog instance for chaining
and registers exactly the four shipped models with their expected recognition and synthesis
roles. This is the integration point that ties the SpeechSherpa library to the Speech library's
`IRecognitionModel`/`ISynthesisModel` extension seam.

Below the system level, each model's identity, download descriptor, engine configuration, and
installation behavior is verified deterministically against small synthetic `.tar.bz2` fixtures,
with no network access and no native runtime (see _SpeechSherpa ModelManagementSubsystem
Verification_). The real, native-backed recognition backend is verified directly against real,
installed models and the real native sherpa-onnx runtime, because mocking native inference would
prove nothing about whether real audio decodes into real text (see _SpeechSherpa
RecognitionSubsystem Verification_).

Automated coverage **does not** extend to a real, multi-hundred-megabyte model download, to
synthesizing real, intelligible speech, or to playing or capturing audio through real hardware.
The real-native recognition tests self-skip when their target model is not installed in the
running environment. The real, native-backed synthesis backend, `SherpaOnnxSynthesisEngine`, is
exercised directly against the real native sherpa-onnx runtime and a real, installed VITS/Piper
synthesis model, self-skipping the same way when that model is not installed (see _SpeechSherpa
SynthesisSubsystem Verification_).

System and unit tests reside in the `DemaConsulting.Speech.Sherpa.Tests` project.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, no network access, and no physical audio hardware; the
  real-native recognition tests additionally use the real sherpa-onnx native runtime and any
  already-installed streaming Zipformer and Nemotron models, self-skipping when absent
- **Isolation**: Each catalog-composing or model-installing test uses a fresh scratch directory,
  so no test ever reads or writes a developer's real installed-model store

## External Interface Simulation

The system tests use no simulation: the real Speech `SpeechModelCatalog` is constructed over an
isolated store root and populated through the real `AddSherpaModels()` extension method. Model
installation is simulated at subsystem level with small synthetic `.tar.bz2` archives built by the
test project's own `TarBz2ArchiveFixtures` helper rather than real production downloads.

## System-Level Test Scenarios

### Integration: AddSherpaModels Returns the Same Catalog Instance

**Test**: `AddSherpaModels_Called_ReturnsSameCatalogInstance`

Verifies that `AddSherpaModels()` returns the exact catalog instance it was called on, so a host
can chain further registration calls.

### Integration: AddSherpaModels Registers the Four Shipped Models

**Test**: `AddSherpaModels_Called_RegistersExpectedFourModelsWithExpectedRoles`

Verifies that calling `AddSherpaModels()` on a real catalog makes exactly the four shipped models -
`SherpaOnnxZipformerEnRecognitionModel`, `SherpaOnnxNemotronStreamingEnRecognitionModel`,
`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`, and `SherpaOnnxKokoroEnglishSynthesisModel` -
appear in the catalog's enumeration with their expected recognition and synthesis roles.

## Acceptance Criteria

A SpeechSherpa system-level test run passes when all scenarios above pass without unexpected
exceptions, every subsystem-level suite passes (with real-native tests either passing or
self-skipping because their model is not installed), and the automated verification boundary
remains honest: catalog registration, model metadata, engine configuration, archive
installation, and real-native recognition and synthesis are claimed as automated coverage, while
real model downloads, real-time playback, and physical audio I/O are left to manual/local
verification.
