### TextEnAnnotations Verification

#### Verification Approach

`TextEnAnnotations` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/TextEnAnnotationsTests.cs`, constructing
`SegmentAnnotation` values directly so the readers are tested independently of the analyzer. Each reader theory holds a
few valid values and one invalid row per distinct failure reason (another kind, a differently cased kind, an empty
value,
a wrong case, a missing separator, an illegal character).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access or files; the culture test restores the current culture

#### Acceptance Criteria

- **Kind names**: the six kinds have their documented, distinct names
- **Readers**: each reader returns the value of an annotation of its own kind that satisfies its grammar and reports
  false, without throwing and with an empty or default result, for another kind (including a differently cased one) or a
  value outside the grammar; a null annotation throws `ArgumentNullException`:
  - part of speech: a Universal Dependencies tag name
  - normalization: the exact spoken text, including a padded value and the empty value that means "speak nothing"
  - spell-out: single capital ASCII letters separated by single spaces with at most one padding space, exactly as stored
  - pronunciation: the scheme (`ipa` or `arpabet`) and the phonemes after the first colon, which follow the
    scheme's syntax
  - replacement: a normalization annotation (never flagged as spelled) or a well-formed spell-out annotation (flagged as
    spelled)
  - reading: a well-formed reading id, not checked against the inventory
  - reading confidence: the invariant number for `0` or `1` optionally followed by a period and one to six ASCII digits,
    under every culture

#### Test Scenarios

##### Kind names

**Test**: `TextEnAnnotations_Kinds_HaveDocumentedDistinctNames`

The six names, `en.norm`, `en.upos`, `en.reading`, `en.reading-confidence`, `en.pron` and `en.spell`, are the documented
wire constants and pairwise distinct.

##### Part-of-speech and normalization readers

**Tests**: `TextEnAnnotations_TryReadPartOfSpeech_PartOfSpeechAnnotation_ReturnsTagName`,
`TextEnAnnotations_TryReadPartOfSpeech_OtherKindOrUnknownValue_ReturnsFalse`,
`TextEnAnnotations_TryReadNormalized_NormalizationAnnotation_ReturnsSpokenText`,
`TextEnAnnotations_TryReadNormalized_OtherKind_ReturnsFalse`,
`TextEnAnnotations_TryReadNormalized_NullAnnotation_ThrowsArgumentNullException`

`NOUN` and `X` are read back; a lower-case or empty value, or the differently cased kind `EN.UPOS`, is not a
part-of-speech annotation. A normalization value with surrounding spaces and the empty value are returned exactly; the
kinds `en.upos`, `EN.NORM` and `en.spell` are not normalizations; a null annotation throws.

##### Spell-out, pronunciation and replacement readers

**Tests**: `TextEnAnnotations_TryReadSpellOut_WellFormedLetters_ReturnsValueExactly`,
`TextEnAnnotations_TryReadSpellOut_OtherKindOrMalformedValue_ReturnsFalse`,
`TextEnAnnotations_TryReadPronunciation_WellFormedValue_ReturnsSchemeAndPhonemes`,
`TextEnAnnotations_TryReadPronunciation_OtherKindOrMalformedValue_ReturnsFalse`,
`TextEnAnnotations_TryReadReplacement_NormAndSpell_ReturnValueAndSpelledFlag`,
`TextEnAnnotations_TryReadReplacement_OtherKindMalformedOrNull_ReturnsFalseOrThrows`

`F B I` and a padded `A B` (with its padding spaces) are returned as stored; another kind, an empty value, lower case,
joined letters and a
hyphen are not spell-outs. An IPA value holding a second colon keeps it in the phonemes and an ARPAbet value is split
into its scheme and phones; another kind, an empty value, a missing phoneme, an upper-case scheme, a control character
in
IPA and a stress digit 3 are not pronunciations. The replacement reader reads a normalization (`Doctor`), the empty
normalization and a padded spell-out and flags only the spell-out as spelled; a part-of-speech annotation, a
pronunciation annotation and a malformed spell-out give false, and a null annotation throws from the replacement, the
spell-out and the pronunciation readers.

##### Reading and confidence readers

**Tests**: `TextEnAnnotations_TryReadReading_WellFormedId_ReturnsId`,
`TextEnAnnotations_TryReadReading_OtherKindOrIllFormedId_ReturnsFalse`,
`TextEnAnnotations_TryReadReadingConfidence_ValidValue_ReturnsInvariantNumberUnderEveryCulture`,
`TextEnAnnotations_TryReadReadingConfidence_InvalidValueOrKind_ReturnsFalse`

`record.noun` and the unknown but well-formed `unknown.reading` are read back (the inventory is not consulted); another
kind, an empty value, one segment, upper case, a pipe and an edge hyphen are not readings. The confidences `0`, `1.000`
and `0.123456` are read back as the invariant number under several cultures; `1.001`, a negative value, an exponent, a
comma separator, seven decimals, a trailing space, a missing digit after the period, an empty value, Arabic-Indic digits
and the kind `en.reading` are not confidences.
