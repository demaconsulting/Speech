### KokoroPhonemeVocabulary Verification

#### Verification Approach

`KokoroPhonemeVocabulary` is verified by automated unit tests in
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests/ModelManagementSubsystem/KokoroPhonemeVocabularyTests.cs`
against this package's own embedded `Resources/kokoro-v1.0-tokenizer.json` resource. Token ids
asserted (`'a'` = 43, `'z'` = 68) were confirmed directly against that embedded resource, not
assumed; `'B'` was confirmed absent from the same vocabulary, standing in for a
phonemizer-produced character the model has no embedding for.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.Kokoro.Tests`
- **Isolation**: no test requires network access; every test uses the same embedded vocabulary
  resource shipped in the production assembly

#### Acceptance Criteria

Known, in-vocabulary characters convert to their confirmed token ids, in order; a character absent
from the embedded vocabulary is silently dropped rather than throwing or inserting a placeholder
id; an empty phoneme string returns an empty token id list; a phoneme string longer than
`MaxPhonemeTokens` is truncated to exactly that many token ids; `PadTokenId` is the constant value
`0`; and a `null` phonemes argument throws `ArgumentNullException`.

#### Test Scenarios

##### Known characters convert to their confirmed token ids, in order

**Test**: `ToTokenIds_KnownChars_ReturnsConfirmedIdsInOrder`

##### An unknown character is skipped rather than substituted

**Test**: `ToTokenIds_UnknownChar_IsSkipped`

##### An empty string returns an empty token id list

**Test**: `ToTokenIds_EmptyString_ReturnsEmptyList`

##### A null phonemes argument throws ArgumentNullException

**Test**: `ToTokenIds_NullPhonemes_ThrowsArgumentNullException`

##### A phoneme string longer than MaxPhonemeTokens is truncated

**Test**: `ToTokenIds_LongerThanMaxPhonemeTokens_IsTruncated`

##### PadTokenId is zero

**Test**: `PadTokenId_IsZero`
