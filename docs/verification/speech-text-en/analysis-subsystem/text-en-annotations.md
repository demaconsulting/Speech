### TextEnAnnotations Verification

#### Verification Approach

`TextEnAnnotations` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/TextEnAnnotationsTests.cs`, constructing
`SegmentAnnotation` values directly so the reader is tested independently of the analyzer.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access or files

#### Acceptance Criteria

The six kinds have their documented, distinct names; a part-of-speech annotation with a Universal
Dependencies value is read back; an annotation of another kind, with a lower-case, longer or empty value, or
with a differently cased kind is reported as not a part-of-speech annotation without throwing; a normalization
annotation is read back with its exact spoken text, including a padded value, the apostrophe and the empty
value that means "speak nothing"; an annotation of another kind or a differently cased or padded kind is
reported as not a normalization without throwing; and a null annotation throws `ArgumentNullException` from
either reader. A spell-out annotation whose value is single capital ASCII letters separated by single spaces, with at
most one padding space on either side, is read back exactly as stored; an annotation of another kind (including a
differently cased kind) or a lower-case, joined, double-spaced, tab-separated, over-padded, hyphenated, accented,
word-containing, empty or blank value is reported as not a spell-out without throwing, with an empty result. A
pronunciation annotation is split at the first colon into a scheme (`ipa` or `arpabet`) and phonemes that follow that
scheme's syntax; another kind, an unknown, upper-case or missing scheme, or malformed phonemes are reported as not a
pronunciation without throwing, with the default scheme and empty phonemes. The combined replacement reader accepts
a normalization annotation (including the empty one) and a well-formed spell-out annotation, flags only the spelled
one, and reports false, without throwing, for any other kind or a malformed spelled value; a null annotation throws
`ArgumentNullException` from the replacement, spell-out and pronunciation readers as well. A reading annotation with a
well-formed reading id (two or three dot-separated segments of lower-case letters, digits and inner hyphens) is read
back exactly, and the id is not checked against the inventory (an unknown but well-formed id is accepted); another kind,
a differently cased kind or an ill-formed id (upper case, a space, a pipe, a colon, a missing or an extra segment, an
edge hyphen, an empty value) is reported as not a reading without throwing. A reading-confidence annotation is read back
as the invariant number for `0`, `1`, and `0` or `1` followed by a period and one to six ASCII digits, under every
culture, and a value above 1, negative, signed, not a number, in exponent form, with whitespace, with a comma separator,
with a missing digit, with too many decimals, with non-ASCII digits or empty, or another kind, is reported as not a
confidence without throwing. The two reading kinds are the ones the two readers accept.

#### Test Scenarios

##### Kind names

**Test**: `TextEnAnnotations_Kinds_HaveDocumentedDistinctNames` (the six names in their documented order,
`en.norm`, `en.upos`, `en.reading`, `en.reading-confidence`, `en.pron` and `en.spell`)

##### Reading a part of speech

**Test**: `TextEnAnnotations_TryReadPartOfSpeech_PartOfSpeechAnnotation_ReturnsTagName`

##### Not a part-of-speech annotation

**Test**: `TextEnAnnotations_TryReadPartOfSpeech_OtherKindOrUnknownValue_ReturnsFalse`

##### Null annotation

**Test**: `TextEnAnnotations_TryReadPartOfSpeech_NullAnnotation_ThrowsArgumentNullException`

##### Reading a normalization

**Test**: `TextEnAnnotations_TryReadNormalized_NormalizationAnnotation_ReturnsSpokenText` (a theory over
`Doctor`, a value with surrounding spaces, an apostrophe and the empty value)

##### Not a normalization

**Test**: `TextEnAnnotations_TryReadNormalized_OtherKind_ReturnsFalse` (a theory over `en.upos`, an upper-case
kind, a kind with a trailing space and `en.spell`)

##### Null annotation for the normalization reader

**Test**: `TextEnAnnotations_TryReadNormalized_NullAnnotation_ThrowsArgumentNullException`

##### Reading a spell-out

**Test**: `TextEnAnnotations_TryReadSpellOut_WellFormedLetters_ReturnsValueExactly` (a theory over `F B I`, one
letter, values with a leading, a trailing and both padding spaces, and `P H D`)

##### Not a spell-out

**Test**: `TextEnAnnotations_TryReadSpellOut_OtherKindOrMalformedValue_ReturnsFalse` (a theory over the normalization
kind, an upper-case kind and malformed values: empty, blank, lower case, joined letters, double spacing, a
tab, excessive padding, a hyphen, an accented letter and a word)

##### Reading a pronunciation

**Test**: `TextEnAnnotations_TryReadPronunciation_WellFormedValue_ReturnsSchemeAndPhonemes` (a theory over IPA and
ARPAbet values, including an IPA value that holds a second colon, which stays in the phonemes)

##### Not a pronunciation

**Test**: `TextEnAnnotations_TryReadPronunciation_OtherKindOrMalformedValue_ReturnsFalse` (a theory over another
kind and malformed values: empty, no scheme, no phonemes, upper-case or unknown scheme, a leading space,
whitespace or a control character in IPA, and lower-case, double-spaced, over-long or stress-digit-3 ARPAbet)

##### Reading a replacement

**Tests**: `TextEnAnnotations_TryReadReplacement_NormAndSpell_ReturnValueAndSpelledFlag`,
`TextEnAnnotations_TryReadReplacement_OtherKindMalformedOrNull_ReturnsFalseOrThrows`

The first test reads a normalization (`Doctor`), the empty normalization and a padded spell-out and checks the value
and that only the spell-out is flagged as spelled. The second test shows that a part-of-speech annotation, a
pronunciation annotation and a malformed spell-out give false with an empty value and no spelled flag, and that a
null annotation throws `ArgumentNullException` from the replacement reader and, in the same test, from the
spell-out and the pronunciation readers.

##### Reading a reading id

**Test**: `TextEnAnnotations_TryReadReading_WellFormedId_ReturnsId` (a theory over `record.noun`, `abstract.adj-noun`,
`a.b.c` and the unknown but well-formed `unknown.reading`)

##### Not a reading

**Test**: `TextEnAnnotations_TryReadReading_OtherKindOrIllFormedId_ReturnsFalse` (a theory over another kind, a
differently cased kind and the ill-formed values: empty, one segment, upper case, a trailing space, a space, a pipe, a
colon, four segments and an edge hyphen)

##### Reading a confidence

**Test**: `TextEnAnnotations_TryReadReadingConfidence_ValidValue_ReturnsInvariantNumberUnderEveryCulture` (a theory over
`0.000`, `1.000`, `1`, `0`, `0.5`, `0.873`, `0.123456`, `1.0` and `1.000000`, read under several cultures)

##### Not a confidence

**Test**: `TextEnAnnotations_TryReadReadingConfidence_InvalidValueOrKind_ReturnsFalse` (a theory over `1.001`, `2`,
`-0.1`, `+0.5`, `NaN`, `1e-1`, a leading or a trailing space, `0,5`, `.5`, `0.`, seven decimals, Arabic-Indic digits,
the empty value and the other kinds `en.reading` and `en.upos`)

##### Reading kinds agree with the readers

**Test**: `TextEnAnnotations_ReadingKinds_AreTheOnesTheReadersAccept`

A reading and a confidence annotation built from the two kind constants are accepted by their own reader and rejected by
the other one.
