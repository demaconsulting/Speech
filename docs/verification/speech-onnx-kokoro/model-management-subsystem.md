## ModelManagementSubsystem Verification

### Verification Approach

**This subsystem has no automated unit test project** - there is no
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests` directory, and none of the requirements below link
to a test, because none exists. This is a known, pre-existing gap recorded honestly here rather
than masked with fictitious test names (see _SpeechOnnxKokoro System Verification_ for the
system-level statement of this gap).

Correctness of every unit in this subsystem rests on manual verification and code review today:

- **`SpeechModelCatalogKokoroExtensions`**: reviewed for correct delegation to
  `SpeechModelCatalog.AddModels` and correct `ArgumentNullException` behavior for a null catalog
- **`OnnxKokoroEnglishSynthesisModel`**: its declared `Id`/`DisplayName`/`Role`/`LicenseName`/
  `LicenseUrl`/`AudioTagSupport`/voice `ChoiceParameter` options were reviewed against the real
  upstream `onnx-community/Kokoro-82M-v1.0-ONNX` model card and `tokenizer.json`; its
  `DownloadDescriptor` file list, relative install paths, and SHA-256 checksums were reviewed
  against the real downloaded bytes recorded in this class's own XML documentation; its
  `CreateBackend`/`ResolveSpeakerId` implementations were reviewed by inspection against the
  sibling SpeechSherpa system's equivalent, proven `SherpaOnnxKokoroEnglishSynthesisModel`
  pattern
- **`KokoroLexiconPhonemizer`**: its embedded lexicon's provenance (produced by running the real
  `misaki.en.G2P` tool offline over the CMUdict word list) and its tokenization/lookup/fallback
  logic were reviewed by direct code inspection; its documented Stage-1 limitations
  (out-of-vocabulary words, homographs) were confirmed to be honestly and completely described in
  its own XML documentation
- **`KokoroPhonemeVocabulary`**: its `tokenizer.json` parsing and `MaxPhonemeTokens`/`PadTokenId`
  constants were reviewed against the real embedded resource and the model's own published
  512-token context window

A future pass may add `test/DemaConsulting.Speech.Onnx.Kokoro.Tests`, mirroring the sibling
SpeechSherpa package's `DemaConsulting.Speech.Sherpa.Tests` conventions. Adding that project is
explicitly out of scope for this pass.

### Test Environment

N/A - no automated test project exists for this subsystem.

### Acceptance Criteria

There is no automated acceptance criterion for this subsystem today. Each unit is accepted on the
strength of: successful compilation with zero warnings, static analysis via
`Microsoft.CodeAnalysis.NetAnalyzers` and `SonarAnalyzer.CSharp`, and the manual/code-review
verification described above.

### Test Scenarios

N/A - no automated test scenarios exist for this subsystem. See each unit's own verification
document for the manual/code-review verification performed in lieu of automated test scenarios:
`onnx-kokoro-english-synthesis-model.md`, `kokoro-lexicon-phonemizer.md`,
`kokoro-phoneme-vocabulary.md`.

The `SpeechModelCatalogKokoroExtensions` unit's manual verification is recorded above; see
_SpeechOnnxKokoro System Verification_.
