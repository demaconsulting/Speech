### SpeechModelCatalogChatterboxTurboExtensions Verification

#### Verification Approach

Verified by `SpeechModelCatalogChatterboxTurboExtensionsTests.cs` against a real `SpeechModelCatalog` whose
store is rooted in a per-test scratch directory. The model is not loaded, so no ONNX files are needed; the
end-to-end path through the Speech pipeline is covered by the system tests.

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: no network; a scratch directory under the temporary path, deleted after each test

#### Acceptance Criteria

Registration adds exactly one model with identity `chatterbox-turbo-onnx-en` and returns the same catalog
instance; a null catalog throws `ArgumentNullException`; with no library in the options the model's voice
choice lists the voices of the catalog store's user folder and the options object is left unmodified; the
variants and voices named by the options are reflected by the registered model.

#### Test Scenarios

##### Registration adds the model and returns the catalog

**Test**: `SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_RegistersModelAndReturnsCatalog`

##### A null catalog is rejected

**Test**: `SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_NullCatalog_ThrowsArgumentNullException`

##### The voice library defaults to the catalog store

**Test**: `SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_DefaultsToCatalogStoreVoices`

This unit owns the verification of that test; the model unit and subsystem documents refer to it only by
description.

##### Variants and voices from the options are composed

**Test**: `SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_VariantsAndVoices_AreComposed`

An Fp32 language model with Fp16 auxiliary graphs and two explicit voices registers a model whose id names
that variant, whose descriptor lists eight files and whose voice choice offers exactly those voices.
