## ModelManagementSubsystem Verification

### Verification Approach

The SpeechOnnxNemotronStt ModelManagementSubsystem is verified through deterministic unit tests in
`test/DemaConsulting.Speech.Onnx.NemotronStt.Tests` that never touch the network or the real
~790 MB production download:

- **`SpeechModelCatalogNemotronSttExtensions`**: verified against a real Speech `SpeechModelCatalog`
  for returning the same catalog instance, registering exactly the one recognition model, and
  accepting preferred execution provider names - see
  `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/SpeechModelCatalogNemotronSttExtensionsTests.cs`
  (`AddNemotronSttModels_Called_ReturnsSameCatalogInstance`,
  `AddNemotronSttModels_Called_RegistersOneRecognitionModel`,
  `AddNemotronSttModels_WithProviders_Registers`). This subsystem folds the extension method in as
  its own review unit (there is no separate unit document), matching the sibling Kokoro system
- **`OnnxNemotronMultilingualRecognitionModel`**: its identity, license, 11-file download
  descriptor, language `ChoiceParameter`, `CreateBackend` validation, and the validation of the
  engine-options overload - see *SpeechOnnxNemotronStt OnnxNemotronMultilingualRecognitionModel
  Verification*
- **`NemotronVocabulary`**: its loading, language-id derivation, blank id, detokenization, and
  word-start detection - see *SpeechOnnxNemotronStt NemotronVocabulary Verification*

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
- **Isolation**: Each test uses a unique scratch directory; no test of this subsystem requires
  network access or the real model files (the real model is exercised only by the system tests)

### Acceptance Criteria

A SpeechOnnxNemotronStt ModelManagementSubsystem test run passes when: `AddNemotronSttModels()` returns
the same catalog instance and registers exactly the one model; the model declares its documented
identity, license (`OpenMDW-1.1`), 11-file download descriptor, and `language` parameter;
`CreateBackend` fails with a clear exception for an empty directory, missing files, and a
vocabulary lacking the selected locale; the engine-options overload rejects null options and an
invalid blank penalty before reading any file; and `NemotronVocabulary` loads tokens, derives
language ids and the blank id, detokenizes text, and detects word starts as documented.

### Test Scenarios

See each unit's own verification document for its detailed test scenarios:
`onnx-nemotron-multilingual-recognition-model.md` and `nemotron-vocabulary.md`. The extension
method's scenarios are recorded above and in *SpeechOnnxNemotronStt System Verification*.
