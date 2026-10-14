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
- **License**: the upstream model is dual-licensed under the MIT License and the NVIDIA Open
  Model License (confirmed by the project maintainer), and both apply. The declared license,
  "MIT and NVIDIA Open Model License", names both. The upstream Hugging Face model card was
  unreachable from the development sandbox, so the declaration was not cross-checked against it.

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
Covers `SpeechOnnxNemotronStt-Models-AddNemotronSttModelsChaining`.

### Integration: AddNemotronSttModels Registers the One Shipped Model

**Test**: `AddNemotronSttModels_Called_RegistersOneRecognitionModel`

Verifies that the catalog enumeration contains exactly the one recognition model.
Covers `SpeechOnnxNemotronStt-Models-AddNemotronSttModels`.

### Integration: AddNemotronSttModels Accepts Execution Provider Names

**Test**: `AddNemotronSttModels_WithProviders_Registers`

Verifies that supplying preferred execution provider names still registers the model. Together
with the registration scenario above, this covers `SpeechOnnxNemotronStt-Models-AddNemotronSttModels`.

### Integration: the registered model is a real, downloadable recognition model

**Tests**: `Metadata_IsDeclared`, `DownloadDescriptor_ListsElevenFiles`

Covers `SpeechOnnxNemotronStt-Models-RecognitionModel`. Verifies the registered model declares
its catalog identity, audio format, and license, and lists all eleven files, each with a
checksum and an HTTPS URI. See _SpeechOnnxNemotronStt ModelManagementSubsystem Verification_.

### Integration: the host selects the recognition language

**Test**: `Parameters_LanguageChoice`

Covers `SpeechOnnxNemotronStt-Models-SelectableLanguage`. Verifies the model offers an en-US
(default) or en-GB language choice without a second set of model files.

### Integration: language and blank ids come from the model's vocabulary

**Tests**: `TryGetLanguageId_KnownLocales_ReturnsOrdinal`, `BlankId_IsBlankTokenIndex`

Covers `SpeechOnnxNemotronStt-Models-VocabularyDerivedIds`. Verifies the language id and blank
token id match the positions declared in the vocabulary rather than fixed values.

### Integration: emitted tokens become readable text

**Test**: `Detokenize_Tokens_NormalizesText`

Covers `SpeechOnnxNemotronStt-Models-ReadableTranscript`. Verifies token pieces and locale
markers are converted into normalized text.

### Integration: an unusable installation is rejected

**Tests**: `CreateBackend_EmptyDirectory_Throws`, `CreateBackend_MissingFiles_Throws`,
`CreateBackend_VocabularyWithoutLocale_Throws`

Covers `SpeechOnnxNemotronStt-Models-UnusableInstallationRejected`. Verifies backend creation
fails with an exception, rather than returning a partial recognizer, for an empty directory,
missing files, and a vocabulary lacking the selected locale.

### Integration: streamed audio is transcribed

**Tests**: `Extract_Chunked_EqualsOneShot`, `Decode_Tokens_AreCollectedAndAdvanced`,
`TryDecode_SpeechChunk_ReportsProvisionalText`

Covers `SpeechOnnxNemotronStt-Recognition-StreamingTranscription`. Verifies features are
chunk-invariant, tokens are decoded greedily, and an engine over the pipeline reports text.
See _SpeechOnnxNemotronStt RecognitionSubsystem Verification_.

### Integration: quiet, isolated words are recovered

**Tests**: `SelectToken_BlankWithinPenalty_PrefersNonBlank`, `PredictToken_ScoreEqualsBlank_AppliesPenalty`

Covers `SpeechOnnxNemotronStt-Recognition-QuietWordRecovery`. Verifies a non-blank token wins
when blank leads by less than the penalty.

### Integration: provisional text is reported once per change

**Tests**: `TryDecode_SpeechChunk_ReportsProvisionalText`, `TryDecode_NoNewTokens_ReportsNothing`,
`TryDecode_QuietAfterShortSpeech_ReportsBeforeEndpoint`

Covers `SpeechOnnxNemotronStt-Recognition-ProvisionalText`. Verifies changed text is reported,
unchanged text is not, and the last words are reported before the endpoint.

### Integration: final text is reported at the end of an utterance

**Tests**: `TryFlush_PartialAudio_ReturnsFinalText`, `TryDecode_QuietAfterSpeech_EndpointsAndResets`,
`TryDecode_OnlySilence_NeverEndpoints`

Covers `SpeechOnnxNemotronStt-Recognition-FinalText`. Verifies flush and a quiet period after
speech each produce a final result, and silence alone never does.

### Integration: each session starts clean

**Test**: `Reset_ClearsState`

Covers `SpeechOnnxNemotronStt-Recognition-SessionIsolation`. Verifies reset discards pending
results and restores encoder and decoder state.

### Integration: native model resources are released on disposal

**Test**: `Dispose_DisposesCollaboratorsAndBlocksUse`

Covers `SpeechOnnxNemotronStt-Recognition-ModelResourceRelease`. Verifies disposal releases the
encoder and decoder once and later use throws.

### Integration: leading silence and low-level noise are mitigated

**Tests**: `Process_DigitalSilence_TruncatesToCap`, `Process_LowLevelNoise_TruncatesToCap`

Covers `SpeechOnnxNemotronStt-Recognition-LeadingSilenceMitigation`. Verifies long quiet runs
are truncated to 400 ms.

### Integration: speech samples survive limiting

**Tests**: `Process_SilenceThenTone_KeepsAllToneSamples`, `Process_VeryQuietSpeech_IsNotDropped`

Covers `SpeechOnnxNemotronStt-Recognition-SpeechPreservedWhenLimiting`. Verifies every speech
sample passes through the limiter.

### Integration: limited audio is dithered deterministically

**Tests**: `Apply_SameSeed_IsDeterministicAndResettable`, `Apply_Statistics_MatchAmplitude`

Covers `SpeechOnnxNemotronStt-Recognition-LeadingSilenceDithering`. Verifies the dither is
reproducible and its standard deviation is within 10% of the configured amplitude.

See _SpeechOnnxNemotronStt RecognitionSubsystem Verification_ for the quiet-word, text,
session, limiting, and dither scenarios.

## Acceptance Criteria

A SpeechOnnxNemotronStt system-level test run passes when all scenarios above pass without unexpected
exceptions and every subsystem-level suite passes (with the one real-model test either passing or
self-skipping because the real model is not installed). The automated boundary remains honest:
catalog registration, model metadata, vocabulary, feature extraction, greedy decoding, limiting,
and engine behavior against fakes are claimed as automated coverage, while real recognition
accuracy, GPU execution, and physical audio I/O are left to manual verification.
