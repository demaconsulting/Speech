### NormalizationAlignment Verification

#### Verification Approach

`NormalizationAlignment` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/NormalizationAlignmentTests.cs`. The unit has no
collaborator except the Speech library's placeholder constant, so no test double is needed: every test builds a small
text, adds spans, pronunciation marks, break edits and clitic marks to an `AlignedTextBuilder` directly, and checks what
is accepted, what is rejected and what the immutable `AlignedText` reports. The tokenizer is not involved. The segment
partition and the application of the spoken forms are test-side helpers: `Fakes/NormalizationHarness.cs` provides them
as extension methods (`Segments()` and `ApplySpokenForms()`) over the result's span list, so they are tested here
through the harness and are not production members. The independent properties of the alignment contract on real, random
text are verified one level up, by the subsystem property tests (see *NormalizationSubsystem Verification*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access or files; each test builds its own builder

#### Acceptance Criteria

**Span validation.** The builder rejects, with `ArgumentException`, a span or a pronunciation mark that overlaps or
precedes the previous one, is empty or leaves the text (an offset that could overflow is rejected without wrapping),
starts or ends on whitespace, covers a tag placeholder, cuts a surrogate pair at either edge, or has a null,
placeholder-containing, or wrongly empty (or wrongly non-empty for a dropped span) replacement or value; it accepts
ascending and touching spans and a whole surrogate pair, and spans and marks form one ascending, disjoint sequence in
either order. A spelled span must carry a letter sequence (single capital letters separated by single spaces, with at
most one padding space per side) while an unflagged span may have any text. A null source throws
`ArgumentNullException`. The segments tile the text exactly (an empty text has none and a text without spans is one gap)
and the spoken-form application replaces only the spans.

**Break reconciliation.** A break edit must be inside the text, after its first character, on a token start and not on a
tag placeholder, otherwise `ArgumentOutOfRangeException`; the built break lists are sorted and free of duplicates
whatever the request order; `ReconcileBreaks` unions the tokenizer breaks with the forced ones and removes the
suppressed ones, ascending and unique, returns the tokenizer's breaks when there is no edit and rejects a null list.

**Counters.** Clitic marks must be ordered, in bounds, non-empty and classified, and the ambiguity, numeric fallback and
the two override counters (applied and ignored) accumulate independently and are zero when none was counted; the
adjacency query tells whether the previous span ends exactly at an offset with a letter.

#### Test Scenarios

##### Span ordering, edges and content

**Tests**: `AlignedTextBuilder_Add_SpanOutOfOrderOverlappingEmptyOrOutOfBounds_ThrowsArgumentException` (a theory over
invalid spans), `AlignedTextBuilder_Add_AscendingDisjointSpans_AreKeptInOrder`,
`AlignedTextBuilder_Add_SpanWithWhitespaceEdge_ThrowsArgumentException`,
`AlignedTextBuilder_Constructor_NullSource_ThrowsArgumentNullException`

A second span that overlaps or precedes the first, has no length, a negative length or start, or ends past the text is
rejected; three ascending spans, two of them touching, come back in order. One test covers the edge facts: a span that
starts or ends on the space of `abc def`, covers a placeholder, or ends or starts inside an emoji's surrogate pair is
rejected while one covering the pair whole is accepted, and a null replacement, one with a placeholder, an empty one for
a symbol and a non-empty one for a dropped span are rejected while an empty dropped replacement is kept.

##### Spelled spans

**Tests**: `SpelledGrammar_IsLetterSequence_AcceptsOnlyCapitalLettersWithSingleSpaces` (an eight-row theory),
`AlignedTextBuilder_Add_SpelledSpan_RequiresLetterSequence`

The grammar accepts `A` and a padded `A B` and rejects empty and blank values, lower case, joined letters, double
spacing and an accented letter. The builder rejects a spelled span whose value is lower case, joined, double-spaced or a
word, accepts a spelled span with and without padding of kind `Initialism` or `Override`, and accepts an unflagged span
that merely has the letter shape.

##### Pronunciation marks

**Tests**: `AlignedTextBuilder_AddPronunciation_EmptyOrOutOfBoundsOrOverflowing_ThrowsArgumentException` (a four-row
theory including `int.MaxValue` starts and lengths), `AlignedTextBuilder_AddPronunciation_UnifiedOrderingWithSpans`,
`AlignedTextBuilder_AddPronunciation_WhitespacePlaceholderSurrogateOrBadValue_ThrowsArgumentException`

An empty, negative, out-of-bounds or overflowing range is rejected. A span, a mark and a second span in ascending order
are accepted; a span overlapping the mark, a mark overlapping or preceding a span and a repeated mark are rejected, and
the same rules hold with a mark first and a span second. A mark that starts or ends on whitespace, covers a placeholder
or cuts a surrogate pair, and a mark with a null, empty or placeholder-carrying value, are rejected, while a mark that
contains a whole surrogate pair is accepted.

##### Partition and spoken text (harness-backed)

**Tests**: `AlignedText_Segments_SpansAndGaps_PartitionTheTextExactly`, `AlignedText_ApplySpokenForms_ReplacesOnlySpans`

The segments of `Dr. Smith & Jones` with two spans are the changed spans and the untouched gaps in order, contiguous and
covering every character; an empty text has no segment and a text without a span is one gap; applying the spoken forms
leaves case, punctuation and whitespace outside the spans as written and drops the empty dropped span.

##### Break edits and reconciliation

**Tests**: `AlignedTextBuilder_ForceBreakAndSuppressBreak_InvalidOffset_ThrowsArgumentOutOfRangeException` (a three-row
theory), `AlignedTextBuilder_ForceBreak_OnPlaceholder_ThrowsArgumentOutOfRangeException`,
`AlignedTextBuilder_Build_BreakRequestsInAnyOrder_AreSortedAndDistinct`,
`AlignedText_ReconcileBreaks_TokenizerForcedAndSuppressed_AreCombined`

An offset at the start, at the end or on whitespace is rejected for both edits, as is one on the placeholder character;
break requests made out of order and repeated come back ascending and unique. With the tokenizer breaking at 6, 9 and
15, forced breaks at 3, 9 and 12 and suppressed breaks at 6 and 12, the reconciled list is 3, 9 and 15; with no edits
the tokenizer's list comes back and a null list throws `ArgumentNullException`.

##### Clitic marks, counters and adjacency

**Tests**: `AlignedTextBuilder_MarkCliticAndCountAmbiguous_ValidateAndAccumulate`,
`AlignedTextBuilder_TouchesPreviousSpokenLetter_ReportsAdjacencyOnlyAfterLetterEndedSpan`

Clitic marks whose range would wrap, exceed the text, overlap the previous mark, are empty or have kind `None` are
rejected while in-order marks are kept (and a rejected mark leaves no state behind). Two ambiguous resolutions, one
numeric fallback, two applied and one ignored override counted on a builder are reported as such by the built
`AlignedText`; a result built with no counts reports zero for the fallback and both override counts, and an
`AlignedText` constructed without pronunciation marks has none. The adjacency query is false before any span, true when
the last span ends exactly at the asked offset and its spoken text ends with a letter, and false after a gap, after a
space-ended value and after a dropped span.
