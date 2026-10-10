### KokoroLexiconPhonemizer Verification

#### Verification Approach

`KokoroLexiconPhonemizer` is verified by automated unit tests in
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests/ModelManagementSubsystem/KokoroLexiconPhonemizerTests.cs`
against this package's own embedded `Resources/kokoro-en-lexicon.tsv.gz` resource, never a mock or
stand-in lexicon. Expected phoneme strings (for `"cat"`, `"dog"`) were read directly from that
embedded resource, not invented, so these tests fail loudly if the embedded resource ever changes.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.Kokoro.Tests`
- **Isolation**: no test requires network access; every test uses the same embedded lexicon
  resource shipped in the production assembly

#### Acceptance Criteria

A single known word phonemizes to its embedded lexicon entry with no unknown words reported;
multiple known words are joined by a single collapsed-whitespace space character; word lookup is
case-insensitive; a vocabulary-declared punctuation character passes through unchanged while a
non-vocabulary punctuation character is dropped; a word absent from the embedded lexicon is
dropped from the phoneme string and reported, in lower-case, as an unknown word; an empty input
produces an empty phoneme string and no unknown words; and a `null` input throws
`ArgumentNullException`.

#### Test Scenarios

##### A known word phonemizes to its embedded lexicon entry with no unknown words

**Test**: `Phonemize_KnownWord_ReturnsLexiconPhonemesWithNoUnknownWords`

##### Multiple known words are joined with a single collapsed-whitespace space

**Test**: `Phonemize_MultipleKnownWords_JoinsWithSingleSpace`

##### An upper-case known word resolves the same as its lower-case spelling

**Test**: `Phonemize_UpperCaseKnownWord_ResolvesSameAsLowerCase`

##### A vocabulary-declared punctuation character passes through unchanged

**Test**: `Phonemize_VocabularyPunctuation_PassesThrough`

##### A non-vocabulary punctuation character is dropped

**Test**: `Phonemize_NonVocabularyPunctuation_IsDropped`

##### An empty input returns an empty phoneme string and no unknown words

**Test**: `Phonemize_EmptyText_ReturnsEmptyPhonemesAndNoUnknownWords`

##### Common contractions are recognized

**Scenario**: `"I don't know"`, `"it’s fine, we can't go"` (typographic apostrophe) and
`"I'm sure they won't"` are converted to phonemes.

**Expected**: no unknown words are reported and the phoneme string is non-empty.

**Test**: `Phonemize_CommonContractions_AreRecognized`

##### Three periods become one ellipsis token

**Scenario**: `"I know..."` is converted to phonemes.

**Expected**: the phoneme string ends with the single ellipsis character and contains no period.

**Test**: `Phonemize_ThreePeriods_BecomeSingleEllipsisToken`

##### An out-of-vocabulary word is dropped and reported as unknown

**Test**: `Phonemize_OutOfVocabularyWord_DroppedAndReportedAsUnknown`

##### A repeated out-of-vocabulary word is reported exactly once, in first-seen order

**Test**: `Phonemize_RepeatedOutOfVocabularyWord_ReportedOnceInFirstSeenOrder`

##### A null text argument throws ArgumentNullException

**Test**: `Phonemize_NullText_ThrowsArgumentNullException`
