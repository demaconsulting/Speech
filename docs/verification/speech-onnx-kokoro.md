# SpeechOnnxKokoro System Verification Design

This document describes the system-level verification strategy for the SpeechOnnxKokoro library.

## Verification Approach

SpeechOnnxKokoro is verified at system level through its one public entry point, the
`AddKokoroModels()` extension method, exercised against a real Speech `SpeechModelCatalog` exactly
as a host composes it: the tests prove the method returns the same catalog instance for chaining
and registers exactly the one shipped model with its expected synthesis role. This is the
integration point that ties the SpeechOnnxKokoro library to the Speech library's
`ISynthesisModel` extension seam.

Below the system level, `OnnxKokoroEnglishSynthesisModel`'s identity, 30-file download descriptor,
and speaker-id resolution are verified deterministically with no network access and no real
production download (see _SpeechOnnxKokoro ModelManagementSubsystem Verification_), as are its
owned text-to-phoneme-to-token pipeline units, `KokoroLexiconPhonemizer` and
`KokoroPhonemeVocabulary`. The real, ONNX-Runtime-backed synthesis backend,
`OnnxKokoroSynthesisEngine`, is verified both against a tiny, hand-built fixture model for its
construction, short-circuit, disposal, and probe behavior, and end-to-end - through
`OnnxKokoroEnglishSynthesisModel.CreateBackend` plus synthetic per-voice style-vector files -
proving that `Generate` forwards the correct voice's style vector verbatim (see _SpeechOnnxKokoro
SynthesisSubsystem Verification_).

Automated coverage **does not** extend to synthesizing real, intelligible speech from the real
~88 MiB production model, or to playing or capturing audio through real hardware. One test,
`Generate_RealInstalledModel_ProducesNonEmptyAudio`, exercises the real production model
end-to-end when it is genuinely installed in the running environment, self-skipping (not failing)
otherwise - see _SpeechOnnxKokoro SynthesisSubsystem Verification_ for the detail.

System and unit tests reside in the `test/DemaConsulting.Speech.Onnx.Kokoro.Tests` project.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, no network access, and no physical audio hardware; one
  test additionally depends on the real, already-installed Kokoro model, self-skipping when absent
- **Isolation**: Each catalog-composing or model-loading test uses a fresh scratch directory, so
  no test ever reads or writes a developer's real installed-model store except the one real-model
  test above, which only reads

## External Interface Simulation

The system tests use no simulation: the real Speech `SpeechModelCatalog` is constructed and
populated through the real `AddKokoroModels()` extension method. Engine construction is simulated
at subsystem/unit level with a tiny, hand-built `.onnx` fixture model and synthetic per-voice
`.bin` style-vector files rather than the real production download.

## System-Level Test Scenarios

### Integration: AddKokoroModels Returns the Same Catalog Instance

**Test**: `AddKokoroModels_Called_ReturnsSameCatalogInstance`

Verifies that `AddKokoroModels()` returns the exact catalog instance it was called on, so a host
can chain further registration calls.

### Integration: AddKokoroModels Registers the One Shipped Model

**Test**: `AddKokoroModels_Called_RegistersExpectedSingleSynthesisModel`

Verifies that calling `AddKokoroModels()` on a real catalog makes exactly the one shipped model,
`OnnxKokoroEnglishSynthesisModel`, appear in the catalog's enumeration with its expected synthesis
role.

### Integration: the registered model declares its identity, 29 voices, and phoneme pipeline

**Tests**: `OnnxKokoroEnglishSynthesisModel_Identity_DeclaresExpectedValues`,
`OnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_KnownVoice_ReturnsDeclarationOrderIndex`,
`Phonemize_KnownWord_ReturnsLexiconPhonemesWithNoUnknownWords`,
`ToTokenIds_KnownChars_ReturnsConfirmedIdsInOrder`

Verifies that the model's declared identity and voice set, its speaker-id resolution, and its
owned phonemizer/vocabulary units all behave as documented - see _SpeechOnnxKokoro
ModelManagementSubsystem Verification_ for the full detail.

### Integration: the real backend generates audio using the selected voice's style

**Tests**: `OnnxKokoroEnglishSynthesisModel_CreateBackend_ValidModelAndVoices_GeneratesUsingSelectedVoiceStyle`,
`RunProbeInference_RunnableSession_DoesNotThrow`

Verifies, through `CreateBackend` and a tiny fixture ONNX model, that the constructed engine's
`Generate` selects and forwards the requested voice's own style vector, and that the
accelerated-provider probe runs successfully against a runnable session - see _SpeechOnnxKokoro
SynthesisSubsystem Verification_ for the full detail.

## Acceptance Criteria

A SpeechOnnxKokoro system-level test run passes when all scenarios above pass without unexpected
exceptions, every subsystem-level suite passes (with the one real-model test either passing or
self-skipping because the real production model is not installed), and the automated verification
boundary remains honest: catalog registration, model metadata, phonemization, tokenization, and
engine construction/style-vector selection against a fixture model are claimed as automated
coverage, while real speech intelligibility and physical audio I/O are left to manual/local
verification.
