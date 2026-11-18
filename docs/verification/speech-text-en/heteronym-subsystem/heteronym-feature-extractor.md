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

- **Deterministic features**: the golden vectors are reproduced exactly under `tr-TR` and `ar-SA`, and the file covers
  the documented edge cases; the FNV-1a values equal the published known answers, the folded hash folds ASCII only and
  the combiner, the feature hash, the salts and the bucket reduction equal reference values; extraction does not
  depend on where the sentence sits in the text; a target at a sentence edge sees the start and end markers; a
  maximal context stays inside the advertised buffer sizes
- **Token description**: the description is culture-independent (the Turkish dotted I is not folded, curly
  apostrophes normalize, digit-only tokens share one form), shape, class, tag and confidence codes follow their
  documented tables per Unicode scalar, and equivalent spellings that the tokenizer splits differently give the same
  token texts and the same features for the target
- **Range safety**: a buffer that is too small, a target outside the sentence and an invalid token range are rejected
  with the documented exception without overflow, and a range that ends exactly at the end of the text is accepted

#### Test Scenarios

##### Deterministic features

**Tests**: `HeteronymFeatureExtractor_Extract_GoldenVectors_MatchFileUnderEveryCulture` (a theory over `tr-TR` and
`ar-SA`), `HeteronymFeatureExtractor_Extract_GoldenVectors_CoverTheDocumentedCases`,
`FeatureHashing_MixAndFeature_ReferenceValues`,
`HeteronymFeatureExtractor_Extract_SentenceOffsetsInText_DoNotChangeFeatures`,
`HeteronymFeatureExtractor_Extract_SentenceEdges_UseMarkerWords`,
`HeteronymFeatureExtractor_Extract_MaximalContext_StaysWithinBuffers`

Every row of the file is rebuilt into context tokens, extracted, and compared with the file's conjoined and shared
hashes, under each culture. The coverage test requires the rows `basic-verb`, `basic-noun`, `sentence-initial`,
`sentence-final-untagged`, `clitic-split`, `curly-apostrophe`, `numbers`, `capitalized`, `all-caps`,
`non-ascii-neighbor`, `supplementary-letter`, `punctuation-neighbors`, `untagged`, `long-bag` and `closed-classes`, in
that order, so the vectors keep covering the edges when rows are edited. The FNV-1a hash of the empty text, `a`,
`foobar` and `<S>` equals the published values; the folded hash of `FooBAR` equals the plain hash of `foobar`, the
dotted capital I does not fold to `i`, an accented capital is unchanged and the Kelvin sign is not folded. The
combiner, three feature hashes, the label and role salts of `record.noun` and `noun`, and the bucket of the feature
hash `0x12345678` under the salt `0x9ABCDEF0` reduced by a 16-bit mask (`0xA7CC`) equal values computed by an
independent reference implementation. The same tokens placed at offset 0 and at offset 5,000 of a text give equal
hashes, so the context is the sentence only. For a target at the first token the word feature at offset -1 is the hash
of the start marker `<S>`, and beyond the end it is the end marker. A sentence of 21 long content words around the
target gives 70 to 96 conjoined and 30 to 48 shared hashes, inside the buffers of 96 and 48.

##### Token description

**Tests**: `HeteronymContext_Create_TurkishDottedI_IsNotFoldedToAscii`,
`HeteronymContext_Create_CurlyApostrophes_NormalizeToPlain`,
`HeteronymContext_Create_DigitOnlyTokens_ShareTheNumberForm`,
`HeteronymContext_Create_Shape_IsDecidedPerScalar`, `HeteronymContext_Create_Class_UsesTheClosedWordLists`,
`HeteronymContext_Create_TagNames_MapToAlphabeticalCodes`,
`HeteronymContext_Create_Confidence_FallsInDocumentedBucket`,
`HeteronymContext_Create_TokenizationParity_EquivalentSpellingsGiveTheSameFeatures`

Under `tr-TR` the capital `I` and `i` have equal lower-case hashes and the dotted capital I does not. Tokens `n't`,
`n’t` and `n‘t` give equal descriptions and the negation class. The tokens `1999`, `42`, `$5` and an Arabic-Indic digit
share the hash of `<num>`, length 5 and class number, while `3rd` does not. The shape theory covers a lower-case word,
an initial capital, digits, a supplementary-plane capital pair (shape 5) and a lone surrogate (not a word). The class
theory covers a determiner, a pronoun, a modal (also `'ll`), an auxiliary, `to`, a negation, a preposition, a
conjunction, a word longer than twelve units that is not a function word and a number, case-insensitively. The 17 tag
names map to codes 1 to 17 and `null` and an unknown name to 0. Confidences 0.99, 0.9, 0.7, 0.69 and NaN fall in the
documented buckets 3, 2, 1, 0 and 0. For `I can't read it.` and `I ca n't read it.`, quoted and spaced quotes, and
`The U.S. record stands.` with and without a space before the period, the real tokenizer gives the same token texts
and the described tokens of the target give the same hashes. The comparison over the whole training corpus is made by
the trainer's evaluation (see the TrainingSubsystem verification).

##### Range safety

**Tests**: `HeteronymFeatureExtractor_Extract_BufferTooSmall_ThrowsInvalidOperationException`,
`HeteronymFeatureExtractor_Extract_TargetOutsideSentence_ThrowsArgumentOutOfRangeException`,
`HeteronymContext_Create_InvalidRange_ThrowsArgumentOutOfRangeException`,
`HeteronymContext_Create_NullTextAndExactEnd_AreHandled`

A conjoined buffer of 4 and a shared buffer of 2 each throw `InvalidOperationException`. A target of -1, 2 or
`int.MaxValue` for a two-token sentence throws `ArgumentOutOfRangeException`. Negative, empty, past-the-end and
overflow-prone token ranges (`start = int.MaxValue`, `length = int.MaxValue`) throw `ArgumentOutOfRangeException`
without arithmetic that could overflow; a null text throws `ArgumentNullException`; a range that ends exactly at the end
of the text is accepted and keeps its start and length.
