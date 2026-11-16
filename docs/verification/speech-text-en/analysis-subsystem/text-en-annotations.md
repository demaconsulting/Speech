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
`ArgumentNullException` from the replacement, spell-out and pronunciation readers as well.

#### Test Scenarios

##### Kind names

**Test**: `TextEnAnnotations_Kinds_HaveDocumentedDistinctNames`

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
