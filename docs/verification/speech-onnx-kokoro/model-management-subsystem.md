## ModelManagementSubsystem Verification

### Verification Approach

The SpeechOnnxKokoro ModelManagementSubsystem is verified through deterministic unit tests in
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests` that never touch the network or the real
~88 MiB production download:

- **`SpeechModelCatalogKokoroExtensions`**: verified against a real Speech `SpeechModelCatalog`
  for correct delegation, returning the same catalog instance, registering exactly the one
  shipped model, and throwing `ArgumentNullException` for a null catalog - see
  `test/DemaConsulting.Speech.Onnx.Kokoro.Tests/SpeechModelCatalogKokoroExtensionsTests.cs`
  (`AddKokoroModels_Called_ReturnsSameCatalogInstance`,
  `AddKokoroModels_Called_RegistersExpectedSingleSynthesisModel`,
  `AddKokoroModels_NullCatalog_ThrowsArgumentNullException`). This subsystem's single file folds
  this extension method in as its own review unit (there is no separate
  `speech-model-catalog-kokoro-extensions.md`), matching the sibling SpeechSherpa system's
  established convention for its own catalog extension method
- **`OnnxKokoroEnglishSynthesisModel`**: its declared identity, license, 29-voice
  `ChoiceParameter`, 30-file download descriptor, preferred audio format, speaker-id resolution
  (including every declared voice plus unknown-value/missing-key/null-bag fallback), and an
  end-to-end `CreateBackend` + `Generate` test against a fake ONNX fixture and 29 synthetic
  per-voice style-vector files proving the correct voice's style vector is forwarded verbatim -
  see _SpeechOnnxKokoro OnnxKokoroEnglishSynthesisModel Verification_
- **`KokoroLexiconPhonemizer`**: its lexicon lookup, whitespace collapsing, vocabulary-punctuation
  passthrough, out-of-vocabulary-word dropping/reporting, and argument validation - see
  _SpeechOnnxKokoro KokoroLexiconPhonemizer Verification_
- **`KokoroPhonemeVocabulary`**: its character-to-token-id lookup, unknown-character dropping,
  truncation to `MaxPhonemeTokens`, and argument validation - see _SpeechOnnxKokoro
  KokoroPhonemeVocabulary Verification_

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.Kokoro.Tests`
- **Isolation**: Each test uses a unique scratch directory under `Path.GetTempPath()`; no test in
  this subsystem requires network access or the real production download
- **Fixtures**: A tiny, hand-built `TestData/fake-kokoro-model.onnx` whose single node forwards
  its `style` input straight through as the output, and synthetic per-voice `.bin` files built by
  the test itself

### Acceptance Criteria

A SpeechOnnxKokoro ModelManagementSubsystem test run passes when: `AddKokoroModels()` returns the
same catalog instance and registers exactly the one shipped model with its expected role and
throws `ArgumentNullException` for a null catalog; `OnnxKokoroEnglishSynthesisModel` declares its
documented identity, license, and 29-voice parameter, its 30-file download descriptor with
well-formed SHA-256 checksums and unique install paths, and its mono 24000 Hz preferred audio
format; `ResolveSpeakerId` resolves every declared voice to its declaration-order index and falls
back to the default voice's index for an unknown value, a missing key, or a `null` bag;
`CreateBackend` throws `ArgumentException` for an empty installed directory and, for a valid
directory, constructs an engine whose `Generate` forwards the requested voice's own style vector
verbatim; `KokoroLexiconPhonemizer.Phonemize` returns the embedded lexicon's phonemes for known
words, collapses whitespace, passes through vocabulary punctuation, drops non-vocabulary
punctuation, drops and reports out-of-vocabulary words, and throws `ArgumentNullException` for
null text; and `KokoroPhonemeVocabulary.ToTokenIds` converts known characters to their confirmed
token ids in order, drops unknown characters, truncates to `MaxPhonemeTokens`, and throws
`ArgumentNullException` for null phonemes.

### Test Scenarios

See each unit's own verification document for its detailed test scenarios:
`onnx-kokoro-english-synthesis-model.md`, `kokoro-lexicon-phonemizer.md`,
`kokoro-phoneme-vocabulary.md`.

The `SpeechModelCatalogKokoroExtensions` unit's test scenarios are recorded above, directly
against `test/DemaConsulting.Speech.Onnx.Kokoro.Tests/SpeechModelCatalogKokoroExtensionsTests.cs`;
see also _SpeechOnnxKokoro System Verification_.
