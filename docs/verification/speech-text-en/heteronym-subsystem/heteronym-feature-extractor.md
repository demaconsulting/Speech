### HeteronymFeatureExtractor Verification

#### Verification Approach

`HeteronymFeatureExtractor`, `HeteronymContext` and `FeatureHashing` are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/HeteronymSubsystem/HeteronymFeatureExtractorTests.cs`. The central evidence is
the golden vector file `TestData/heteronym-feature-vectors.tsv`: each row holds a sentence as `text:TAG:confidence`
items, the index of the target and the exact conjoined and shared hashes in lower-case hexadecimal, so any change of a
template, a code, a word list or a hash changes a row and fails the test, which is the point: a trained model is valid
only while the features are unchanged, and a change needs a new feature-set version and new vectors, reviewed on
purpose. The hash functions are checked against published and independently computed reference values. The token
description is checked with hand-written texts that probe every decision (culture, scalar, range). The
tokenization-parity test uses the real tokenizer of the part-of-speech package (a test-only dependency) on equivalent
spellings. No fake is needed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Files**: the golden vector file, copied next to the test assembly under `TestData`
- **Isolation**: the culture tests set the current culture and restore it in a `finally` block; no test needs network
  access

#### Acceptance Criteria

The golden vectors are reproduced exactly under the invariant, `tr-TR`, `de-DE` and `ar-SA` cultures, and the file
covers the documented edge cases; extraction is a pure function that does not depend on where the sentence sits in the
text; a target at a sentence edge sees the start and end markers; a maximal context stays inside the buffer sizes (the
counts lie between the documented floor and the documented maxima); a buffer that is too small or a target outside the
sentence is rejected with the documented exception; the feature-set version and the buffer sizes are the documented
constants; the FNV-1a values equal the published known answers, the folded hash folds ASCII only, and the combiner, the
feature hash and the salts equal reference values; the token description is culture-independent (the Turkish dotted I is
not folded, curly apostrophes normalize, digit-only tokens share one form, shape and class and tag and confidence codes
follow their documented tables, per Unicode scalar); the span of a token is validated without overflow; and equivalent
spellings that the tokenizer splits differently give the same token texts and the same features for the target.

#### Test Scenarios

##### Golden vectors

**Tests**: `HeteronymFeatureExtractor_Extract_GoldenVectors_MatchFileUnderEveryCulture` (a theory over the invariant
culture, `tr-TR`, `de-DE` and `ar-SA`), `HeteronymFeatureExtractor_Extract_GoldenVectors_CoverTheDocumentedCases`

Every row of the file is rebuilt into context tokens, extracted, and compared with the file's conjoined and shared
hashes, under each culture. The coverage test requires the rows `basic-verb`, `basic-noun`, `sentence-initial`,
`sentence-final-untagged`, `clitic-split`, `curly-apostrophe`, `numbers`, `capitalized`, `all-caps`,
`non-ascii-neighbor`, `supplementary-letter`, `punctuation-neighbors`, `untagged`, `long-bag` and `closed-classes`, in
that order, so the vectors keep covering the edges when rows are edited.

##### Determinism and context

**Tests**: `HeteronymFeatureExtractor_Extract_SameInputTwice_GivesIdenticalHashes`,
`HeteronymFeatureExtractor_Extract_SentenceOffsetsInText_DoNotChangeFeatures`,
`HeteronymFeatureExtractor_Extract_SentenceEdges_UseMarkerWords`

The same sentence extracted twice gives equal hashes in equal order. The same tokens placed at offset 0 and at offset
5,000 of a text give equal hashes, so the context is the sentence only. For a target at the first token the word feature
at offset -1 (template 4) is the hash of the start marker `<S>`, and at offset +3 beyond the end it is the end marker.

##### Bounds and defects

**Tests**: `HeteronymFeatureExtractor_Extract_MaximalContext_StaysWithinBuffers`,
`HeteronymFeatureExtractor_Extract_BufferTooSmall_ThrowsInvalidOperationException`,
`HeteronymFeatureExtractor_Extract_TargetOutsideSentence_ThrowsArgumentOutOfRangeException` (a theory over -1, 2 and
`int.MaxValue` for a two-token sentence), `HeteronymFeatureExtractor_Constants_AreTheDocumentedOnes`

A sentence of 21 long content words around the target gives 70 to 96 conjoined and 30 to 48 shared hashes (the
documented maxima are 78 and 37, inside the buffers of 96 and 48). A conjoined buffer of 4 and a shared buffer of 2 each
throw `InvalidOperationException`. The feature-set version is 1 and the buffer sizes are 96 and 48.

##### Hash functions

**Tests**: `FeatureHashing_Fnv1a_KnownAnswers`, `FeatureHashing_Fnv1aFolded_FoldsAsciiOnly`,
`FeatureHashing_MixAndFeature_ReferenceValues`

The FNV-1a hash of the empty text, `a`, `foobar` and `<S>` equals the published values. The folded hash of `FooBAR`
equals the plain hash of `foobar`, the dotted capital I does not fold to `i`, an accented capital is unchanged and the
Kelvin sign is not folded. The combiner, three feature hashes and the label and role salts of `record.noun` and `noun`
equal values computed by an independent reference implementation.

##### Token description

**Tests**: `HeteronymContext_Create_TurkishDottedI_IsNotFoldedToAscii`,
`HeteronymContext_Create_CurlyApostrophes_NormalizeToPlain`,
`HeteronymContext_Create_DigitOnlyTokens_ShareTheNumberForm`,
`HeteronymContext_Create_Shape_IsDecidedPerScalar` (a theory over twelve texts),
`HeteronymContext_Create_Class_UsesTheClosedWordLists` (a theory over sixteen texts),
`HeteronymContext_Create_TagNames_MapToAlphabeticalCodes`,
`HeteronymContext_Create_Confidence_FallsInDocumentedBucket` (a theory over nine confidences)

Under `tr-TR` the capital `I` and `i` have equal lower-case hashes and the dotted capital I does not. Tokens `n't`,
`n’t` and `n‘t` give equal descriptions and the negation class. The tokens `1999`, `42`, `$5` and an Arabic-Indic digit
share the hash of `<num>`, length 5 and class number, while `3rd` does not. Shapes are 3 for `record`, 6 for `Record`, 5
for `RECORD`, 7 for `rEcord`, 4 for `A`, 2 for digits, 1 for `...` and an emoji and a lone surrogate (not words), a
supplementary-plane capital letter is a capital (shape 4, and 5 for two), and a word with an accented initial capital
has shape 6. The class theory covers a determiner, a pronoun, a modal (also `'ll`), an auxiliary (also `'s`), `to`, a
negation, a preposition (also `underneath`), a conjunction, an ordinary word, a word longer than twelve units that is
not a function word, punctuation and a number, case-insensitively. The 17 tag names map to codes 1 to 17 and `null`, a
lower-case name and an unknown name to 0. Confidences 1.0 and 0.99 are bucket 3, 0.9899 and 0.9 are 2, 0.89 and 0.7 are
1, and 0.69, 0 and NaN are 0.

##### Range validation

**Tests**: `HeteronymContext_Create_InvalidRange_ThrowsArgumentOutOfRangeException` (a theory over nine ranges),
`HeteronymContext_Create_NullTextAndExactEnd_AreHandled`

Negative, empty, past-the-end and overflow-prone ranges (`start = int.MaxValue`, `length = int.MaxValue`) throw
`ArgumentOutOfRangeException` without arithmetic that could overflow; a null text throws `ArgumentNullException`; a
range that ends exactly at the end of the text is accepted and keeps its start and length.

##### Tokenization parity

**Test**: `HeteronymContext_Create_TokenizationParity_EquivalentSpellingsGiveTheSameFeatures` (a theory over seven
pairs)

For `I can't read it.` and `I ca n't read it.`, `They don't record it.` and `They do n't record it.`,
`John's record is good.` and `John 's record is good.`, quoted and spaced quotes, parentheses with and without inner
spaces, `The U.S. record stands.` with and without a space before the period, and `A well-known record.` likewise, the
real tokenizer gives the same token texts and the described tokens of the target give the same hashes, so the trainer
and the analyzer, which may see a text spelled in either way, see one context. The comparison over the whole training
corpus belongs to the trainer increment.
