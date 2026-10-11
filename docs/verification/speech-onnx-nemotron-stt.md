# SpeechOnnxNemotronStt System Verification Design

This document describes the system-level verification strategy for the SpeechOnnxNemotronStt library.

## Verification Approach

SpeechOnnxNemotronStt is verified at system level through its one public entry point, the
`AddNemotronSttModels()` extension method, exercised against a real Speech `SpeechModelCatalog`
exactly as a host composes it: the tests prove the method returns the same catalog instance for
chaining, registers exactly the one shipped recognition model, and accepts a list of preferred
execution provider names. This is the integration point that ties the SpeechOnnxNemotronStt library
to the Speech library's `IRecognitionModel` extension seam.

Below the system level, `OnnxNemotronMultilingualRecognitionModel`'s identity, 11-file download
descriptor, language parameter, and `CreateBackend` validation, plus the owned
`NemotronVocabulary`, are verified deterministically with no network access and no real
production download (see _SpeechOnnxNemotronStt ModelManagementSubsystem Verification_). The
streaming pipeline - `SilenceRunLimiter`, `DitherNoise`, `NemotronFeatureExtractor`,
`NemotronRnntGreedyDecoder`, and `OnnxNemotronRecognitionEngine` - is verified entirely against
managed fakes of the encoder and prediction networks, so the unit tests never depend on model
files (see _SpeechOnnxNemotronStt RecognitionSubsystem Verification_).

Automated coverage **does not** extend to real recognition accuracy or to capturing audio through
real hardware. One test, `CreateBackend_RealModel_TranscribesWhenPresent`, exercises the real
~790 MB production model end-to-end when it is genuinely installed in the running environment,
self-skipping (not failing) otherwise.

System and unit tests reside in the `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests` project.

### Manual Real-Model Results

The following were measured manually on a development machine with the real installed model, on
the CPU, and are recorded here as evidence only; they are not automated coverage:

- The command-line tool run on `tools/NemotronSpike/t1.wav` (13.3 s of audio) produced "The quick
  brown fox jumps over the lazy dog. Yes, wait, I think we should try the new speech recognition
  model today because it might be faster and more accurate". The limiter compresses the long
  pauses, so punctuation differs slightly from the run without the limiter, which produced "Yes,
  wait, I think ... today, because ... accurate."
- Wall-clock real-time factor was about 0.38, including process start and a model load of about
  1.5 to 2 seconds on the CPU.
- Variants of `yes.wav` and `wait.wav` with 0, 0.5, 1, or 3 seconds of exact digital silence, or
  1e-3 uniform noise, before the speech and 1 second of trailing silence all transcribed as "Yes"
  and "Wait, I will be right back" respectively (16 of 16 variants).
- DirectML and other GPU execution providers could not be verified; only CPU execution was
  exercised.
- **UNVERIFIED**: the declared license, "NVIDIA Open Model License", could not be confirmed
  because the upstream Hugging Face model card was unreachable from the development sandbox (its
  front matter may say `mit`). It must be confirmed against the live model card before release.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, no network access, and no physical audio hardware; one
  test additionally depends on the real, already-installed Nemotron model, self-skipping when
  absent
- **Isolation**: Each catalog-composing or model-loading test uses a fresh scratch directory

## External Interface Simulation

The system tests use no simulation: the real Speech `SpeechModelCatalog` is populated through the
real `AddNemotronSttModels()` extension method. The ONNX encoder and the prediction networks are
simulated at unit level by managed fakes (`Fakes.cs`).

## System-Level Test Scenarios

### Integration: AddNemotronSttModels Returns the Same Catalog Instance

**Test**: `AddNemotronSttModels_Called_ReturnsSameCatalogInstance`

Verifies that `AddNemotronSttModels()` returns the exact catalog instance it was called on.

### Integration: AddNemotronSttModels Registers the One Shipped Model

**Test**: `AddNemotronSttModels_Called_RegistersOneRecognitionModel`

Verifies that the catalog enumeration contains exactly the one recognition model.

### Integration: AddNemotronSttModels Accepts Execution Provider Names

**Test**: `AddNemotronSttModels_WithProviders_Registers`

Verifies that supplying preferred execution provider names still registers the model.

### Integration: the registered model declares identity, files, and language choice

**Tests**: `Metadata_IsDeclared`, `DownloadDescriptor_ListsElevenFiles`,
`Parameters_LanguageChoice`

See _SpeechOnnxNemotronStt ModelManagementSubsystem Verification_.

### Integration: the streaming pipeline transcribes, endpoints, and mitigates leading silence

**Tests**: `TryDecode_SpeechChunk_ReportsProvisionalText`, `TryFlush_PartialAudio_ReturnsFinalText`,
`TryDecode_QuietAfterSpeech_EndpointsAndResets`, `Process_DigitalSilence_TruncatesToCap`

See _SpeechOnnxNemotronStt RecognitionSubsystem Verification_.

## Acceptance Criteria

A SpeechOnnxNemotronStt system-level test run passes when all scenarios above pass without unexpected
exceptions and every subsystem-level suite passes (with the one real-model test either passing or
self-skipping because the real model is not installed). The automated boundary remains honest:
catalog registration, model metadata, vocabulary, feature extraction, greedy decoding, limiting,
and engine behavior against fakes are claimed as automated coverage, while real recognition
accuracy, GPU execution, and physical audio I/O are left to manual verification.
