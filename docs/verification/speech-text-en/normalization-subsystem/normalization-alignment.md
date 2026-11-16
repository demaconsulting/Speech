### NormalizationAlignment Verification

#### Verification Approach

`NormalizationAlignment` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/NormalizationAlignmentTests.cs`. The unit has no
collaborator except the Speech library's placeholder constant, so no test double is needed: every test builds
a small text, adds spans, pronunciation marks, break edits and clitic marks to an `AlignedTextBuilder` directly, and checks what is
accepted, what is rejected and what the immutable `AlignedText` reports. The tokenizer is not involved. The
independent properties of the alignment contract on real, random text are verified one level up, by the
subsystem property tests (see _NormalizationSubsystem Verification_).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access or files; each test builds its own builder

#### Acceptance Criteria

The builder rejects, with `ArgumentException`, a span that overlaps or precedes the previous one, is empty or
leaves the text, starts or ends on whitespace, covers a tag placeholder, cuts a surrogate pair at either edge,
or has a null, placeholder-containing, or wrongly empty (or wrongly non-empty for a dropped span) replacement;
it accepts ascending and touching spans and a whole surrogate pair. A break edit must be inside the text after
its first character and on a token start, otherwise `ArgumentOutOfRangeException`; the built break lists are
sorted and free of duplicates whatever the request order. Clitic marks must be ordered, in bounds, non-empty
and classified, and the ambiguity counter, the numeric fallback counter and the two override counters (applied and
ignored) accumulate independently (each is zero when none was counted). A spelled span must carry a letter
sequence (single capital letters separated by single spaces, with at most one padding space per side), and the spelled
flag of a span is false by default. A pronunciation mark must be non-empty, in bounds, free of
whitespace edges, tag placeholders and cut surrogate pairs, and carry a non-empty value without a placeholder; marks
and replacement spans form one ascending, disjoint sequence in either order, and an offset that could overflow is
rejected without wrapping. A null source throws `ArgumentNullException`. The
segments tile the text exactly (an empty text has none and a text without spans is one gap),
`ApplySpokenForms` replaces only the spans, `ReconcileBreaks` unions the tokenizer breaks with the forced ones
and removes the suppressed ones, ascending and unique (a null list throws), a token reports its end and its
characters without copying, and the two public enumerations keep their names and numeric values, including
`NormalizationKind.Override` equal to 10.

#### Test Scenarios

##### Span ordering and bounds

**Tests**: `AlignedTextBuilder_Add_SpanOutOfOrderOverlappingEmptyOrOutOfBounds_ThrowsArgumentException` (a
theory over six invalid spans), `AlignedTextBuilder_Add_AscendingDisjointSpans_AreKeptInOrder`

A second span that overlaps or precedes the first, has no length, a negative length or start, or ends past the
text is rejected; three ascending spans, two of them touching, come back in order.

##### Span edges and content

**Tests**: `AlignedTextBuilder_Add_SpanWithWhitespaceEdge_ThrowsArgumentException` (a theory),
`AlignedTextBuilder_Add_SpanCoveringPlaceholder_ThrowsArgumentException`,
`AlignedTextBuilder_Add_SpanCuttingSurrogatePair_ThrowsArgumentException`,
`AlignedTextBuilder_Add_InvalidSpokenText_ThrowsArgumentException`

A span that starts or ends on the space of `abc def` is rejected; one that covers the placeholder character is
rejected; spans that end or start inside an emoji's surrogate pair are rejected while one covering the pair
whole is accepted; a null replacement, one with a placeholder, an empty one for a symbol and a non-empty one
for a dropped span are rejected, and an empty dropped replacement is kept.

##### Construction

**Test**: `AlignedTextBuilder_Constructor_NullSource_ThrowsArgumentNullException`

##### Break edits

**Tests**: `AlignedTextBuilder_ForceBreakAndSuppressBreak_InvalidOffset_ThrowsArgumentOutOfRangeException` (a
theory), `AlignedTextBuilder_ForceBreak_OnPlaceholder_ThrowsArgumentOutOfRangeException`,
`AlignedTextBuilder_Build_BreakRequestsInAnyOrder_AreSortedAndDistinct`

An offset at the start, negative, at the end or on whitespace is rejected for both edits, as is one on the
placeholder character; break requests made out of order and repeated come back ascending and unique.

##### Clitic marks and the counters

**Tests**: `AlignedTextBuilder_MarkCliticAndCountAmbiguous_ValidateAndAccumulate`,
`AlignedTextBuilder_CountFallback_IsReportedOnTheResult`,
`AlignedTextBuilder_TouchesPreviousSpokenLetter_ReportsAdjacencyOnlyAfterLetterEndedSpan`

Two numeric fallbacks counted on a builder are reported as `FallbackCount` 2 by the built `AlignedText`, a
builder without any gives 0, and the count is independent of the ambiguity count.

The adjacency query is false before any span, true when the last span ends exactly at the asked offset and its
spoken text ends with a letter, and false after a gap, after a space-ended value and after a dropped span.

##### Partition and spoken text

**Tests**: `AlignedText_Segments_SpansAndGaps_PartitionTheTextExactly`,
`AlignedText_Segments_EmptyTextAndNoSpans_YieldNothingAndOneGap`,
`AlignedText_ApplySpokenForms_ReplacesOnlySpans`

The segments of a text with spans are changed spans and untouched gaps in order, contiguous, covering every
character; an empty text has no segment and a text without a span is one gap; applying the spoken forms
leaves case, punctuation and whitespace outside the spans as written.

##### Break reconciliation

**Tests**: `AlignedText_ReconcileBreaks_TokenizerForcedAndSuppressed_AreCombined`,
`AlignedText_ReconcileBreaks_NoEditsAndNull_ReturnsInputAndRejectsNull`

##### Token and enumeration vocabulary

**Tests**: `NormalizationToken_EndAndIn_DescribeTheTokenSpan`,
`NormalizationKindAndCliticKind_Members_KeepNamesAndValues`

The enumeration test asserts the documented names and the numeric values of `NormalizationKind` (1 to 5 as
before, `Number` 6, `Currency` 7, `DateTime` 8 and `Unit` 9, which were added without changing the earlier
members, and `Override` 10, which is the source kind of a user pronunciation override) and `CliticKind` (0 to 11),
which are part of the public contract.

##### Spelled spans

**Tests**: `SpelledGrammar_IsLetterSequence_AcceptsOnlyCapitalLettersWithSingleSpaces`,
`AlignedTextBuilder_Add_SpelledSpan_RequiresLetterSequence`,
`NormalizedSpan_Spelled_DefaultsToFalse`

The grammar theory accepts `A`, `A B`, `F B I` and values with one leading or trailing space, and rejects empty and
blank values, lower case, joined letters, double spacing, a tab, a hyphen, a word, an accented letter and a comma.
The builder rejects a spelled span whose value is lower case, joined, double-spaced or a word, accepts a spelled span
with and without padding of kind `Initialism` or `Override`, and accepts an unflagged span that merely has the
letter shape; the flag of a span is false when it is not given.

##### Pronunciation marks

**Tests**: `AlignedTextBuilder_AddPronunciation_EmptyOrOutOfBoundsOrOverflowing_ThrowsArgumentException` (a theory
over eight ranges, including `int.MaxValue` starts and lengths),
`AlignedTextBuilder_AddPronunciation_UnifiedOrderingWithSpans`,
`AlignedTextBuilder_AddPronunciation_WhitespacePlaceholderSurrogateOrBadValue_ThrowsArgumentException`

An empty, negative, out-of-bounds or overflowing range is rejected. A span, a mark and a second span in ascending
order are accepted; a span overlapping the mark, a mark overlapping or preceding a span and a repeated mark are
rejected, and the same rules hold with a mark first and a span second. A mark that starts or ends on whitespace,
covers a placeholder or cuts a surrogate pair, and a mark with a null, empty or placeholder-carrying value, are
rejected, while a mark that contains a whole surrogate pair is accepted.

##### Override counters

**Test**: `AlignedTextBuilder_OverrideCounters_AreReportedOnTheResult`

Two applied and one ignored override counted on a builder are reported as 2 and 1 by the built `AlignedText`, and an
`AlignedText` constructed without any reports zero for both and no pronunciation marks.
