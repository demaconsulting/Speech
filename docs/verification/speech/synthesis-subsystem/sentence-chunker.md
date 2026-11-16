### SentenceChunker

#### Verification Approach

`SentenceChunker` is a pure static function over strings, so it is verified directly with plain
string inputs and exact expected chunk lists; no test doubles are needed. Because the chunker was
refactored onto one position-aware implementation, behavior preservation is proven by a golden
corpus test whose expected values were captured from the previous implementation before it was
changed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: None
- **Test doubles**: None

#### Acceptance Criteria

The unit is considered verified when text splits on primary then secondary punctuation regardless
of length, with a whitespace-budget fallback and an over-length word kept whole; numerals keep
embedded punctuation; consecutive terminators merge into one boundary; punctuation-only pieces
merge into the preceding chunk or are dropped; ellipsis metadata is reported per chunk; and a
non-positive length or null text is rejected. The refactored implementation is verified as
unchanged for unannotated text by a golden corpus recorded from the pre-refactor implementation.
`ChunkAligned` is verified when each annotation lying wholly inside a chunk is delivered
re-based to the chunk text (through trimming, whitespace collapsing, and punctuation merges), every
annotation that cannot be represented in one chunk is dropped, supplied sentence breaks replace the
punctuation pass while the clause and budget passes still apply, a chunk whose only letters are
supplementary-plane characters is kept on the analyzed path while the no-analysis path keeps its
legacy filtering, and null text or a non-positive
length is rejected.

#### Test Scenarios

All tests are in `test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SentenceChunkerTests.cs`.

##### Boundary Splitting

**Stimulus**: Single and multiple sentences, long clause-bearing sentences, short sentences
containing a comma, semicolon or colon, and a long clause with no punctuation.

**Expected**: Primary then secondary splits, applied even under the length budget; the
whitespace budget applies only to pieces still too long.

**Tests**: `SentenceChunker_Chunk_ShortSingleSentence_ReturnsOneChunk`,
`SentenceChunker_Chunk_MultipleSentences_SplitsOnPrimaryBoundaries`,
`SentenceChunker_Chunk_LongSentenceWithClauses_SplitsOnSecondaryBoundaries`,
`SentenceChunker_Chunk_LongClauseWithNoPunctuation_SplitsOnWhitespaceBudget`,
`SentenceChunker_Chunk_ShortSentenceWithComma_SplitsOnCommaEvenUnderBudget`,
`SentenceChunker_Chunk_ShortSentenceWithSemicolonOrColon_SplitsEvenUnderBudget`

##### Boundary Handling and Argument Validation

**Stimulus**: A single word longer than the budget, a non-positive maximum length, null text.

**Expected**: The word is returned whole; the invalid arguments throw
`ArgumentOutOfRangeException` / `ArgumentNullException`.

**Tests**: `SentenceChunker_Chunk_SingleWordExceedsBudget_ReturnsWholeWord`,
`SentenceChunker_Chunk_NonPositiveMaxLength_ThrowsArgumentOutOfRangeException`,
`SentenceChunker_Chunk_NullText_ThrowsArgumentNullException`

##### Numerals and Consecutive Terminators

**Stimulus**: Times, thousands separators and decimals; runs of `.`, `!`, `?`.

**Expected**: Numeral punctuation (including a leading decimal point) stays attached, while a
letter-preceded period followed by a digit still splits; each terminator run is one boundary,
including at the end of the text.

**Tests**: `SentenceChunker_Chunk_ClausePunctuationEmbeddedInNumeral_StaysAttached`,
`SentenceChunker_Chunk_LeadingDecimalPoint_StaysAttached`,
`SentenceChunker_Chunk_LetterPrecededPeriodFollowedByDigit_Splits`,
`SentenceChunker_Chunk_MidSentenceEllipsis_StaysAttachedAsSingleChunk`,
`SentenceChunker_Chunk_MixedConsecutiveTerminators_StaysAttachedAsSingleChunk`,
`SentenceChunker_Chunk_RepeatedExclamations_StaysAttachedAsSingleChunk`,
`SentenceChunker_Chunk_SingleTerminators_NoRegression`,
`SentenceChunker_Chunk_TrailingEllipsis_NoDegenerateTrailingChunk`

##### Degenerate Pieces and Ellipsis Metadata

**Stimulus**: Punctuation-only runs (spaced dots, lone commas, leading run); trailing ellipses
adjacent and spaced; fewer than three dots.

**Expected**: Degenerate pieces merge or drop; `EndsWithEllipsis` is true only for three or more
dots; `Chunk` and `ChunkWithMetadata` agree on chunk text.

**Tests**: `SentenceChunker_Chunk_SpacedPunctuationRun_MergesIntoPrecedingChunk`,
`SentenceChunker_Chunk_LoneSecondaryBoundaryWithNoWordContent_MergesIntoPrecedingChunk`,
`SentenceChunker_Chunk_LeadingDegenerateRun_IsDropped`,
`SentenceChunker_ChunkWithMetadata_TrailingAdjacentEllipsis_EndsWithEllipsisIsTrue`,
`SentenceChunker_ChunkWithMetadata_TrailingSpacedEllipsis_EndsWithEllipsisIsTrue`,
`SentenceChunker_ChunkWithMetadata_FewerThanThreeDots_EndsWithEllipsisIsFalse`,
`SentenceChunker_Chunk_AndChunkWithMetadata_ProduceSameChunkText`

##### Empty and Whitespace-Only Input

**Stimulus**: Empty text and whitespace-only text.

**Expected**: No chunks.

**Test**: `SentenceChunker_Chunk_EmptyOrWhitespace_ReturnsEmpty`

##### Behavior Preserved: Golden Corpus

**Stimulus**: A representative corpus of at least forty inputs - every pre-existing test string
plus ellipses, `?!` runs, numerals (`12:30`, `1,000`, `.5`, `$.99`, `Wait.5`), spaced dots, leading
and trailing degenerate punctuation, over-budget words, multi-space runs inside an over-budget
clause, tabs and newlines, and a 3000-character text - is chunked. The expected `Text` and
`EndsWithEllipsis` values were recorded from the implementation that existed *before* the
position-aware refactor and are stored as literals.

**Expected**: Byte-for-byte identical chunks and ellipsis flags, which proves the refactor onto the
single position-aware implementation changed nothing for text without annotations or breaks, and
`ChunkAligned` with no annotations and no breaks equals `ChunkWithMetadata` for every corpus input.

**Tests**: `SentenceChunker_Chunk_RepresentativeCorpus_MatchesGoldenChunks`,
`SentenceChunker_ChunkAligned_NoAnnotationsNoBreaks_EqualsChunkWithMetadata`

##### Annotation Re-Basing

**Stimulus**: `ChunkAligned` is given annotations over text that is split into several sentences,
has leading whitespace, is split on a whitespace budget over a multi-space run, and has a
punctuation-only run merged onto a preceding chunk.

**Expected**: Each chunk carries only the annotations lying wholly inside it, with offsets relative
to the chunk text (so `chunk.Text.Substring(Start, Length)` is the annotated word), accounting
for trimming, whitespace collapsed by a budget-split join, and merged punctuation.

**Tests**: `SentenceChunker_ChunkAligned_AnnotationWithinSecondSentence_OffsetRelativeToChunk`,
`SentenceChunker_ChunkAligned_LeadingWhitespace_OffsetAccountsForTrim`,
`SentenceChunker_ChunkAligned_WhitespaceBudgetSplit_AnnotationRebasedThroughCollapsedWhitespace`,
`SentenceChunker_ChunkAligned_DegenerateMerge_EmptyValueAnnotationOnMergedPunctuation_Preserved`

##### Unrepresentable Annotations Are Dropped

**Stimulus**: Annotations that cross a chunk boundary, start or end on trimmed whitespace, lie (with
an empty value) on a dropped leading degenerate run, or lie outside the text.

**Expected**: Each is dropped and no exception is thrown; no annotation is ever misplaced.

**Tests**: `SentenceChunker_ChunkAligned_AnnotationSpanningChunkBoundary_Dropped`,
`SentenceChunker_ChunkAligned_AnnotationOnTrimmedWhitespace_Dropped`,
`SentenceChunker_ChunkAligned_LeadingDegenerateRunEmptyValueAnnotation_Dropped`,
`SentenceChunker_ChunkAligned_AnnotationOutOfRange_Dropped`

##### Analyzer-Supplied Sentence Boundaries

**Stimulus**: `"Dr. Smith arrived. He left."` with a break at offset 18; `"one two"` with a
break at offset 4; `"A. B."` with an empty break list; a comma-bearing sentence with breaks
supplied; and breaks outside the text.

**Expected**: The supplied breaks replace the primary punctuation pass (yielding `"Dr. Smith
arrived."` and `"He left."`, and splitting `"one two"` with no punctuation at all); an empty list
means no sentence splits but the clause pass still applies; the clause and budget passes still
apply within each sub-range; out-of-range breaks are ignored; a `null` list keeps the punctuation
heuristic.

**Tests**: `SentenceChunker_ChunkAligned_AnalyzerBreaks_OverridePunctuationHeuristic`,
`SentenceChunker_ChunkAligned_AnalyzerBreaks_SplitWithoutPunctuation`,
`SentenceChunker_ChunkAligned_EmptyBreaksList_NoPrimarySplitButClauseSplitStillApplies`,
`SentenceChunker_ChunkAligned_BreaksOutOfRangeIgnored`,
`SentenceChunker_ChunkAligned_AnalyzerBreaks_ClauseCommaStillSplits`

##### Scalar-Aware Content Detection

**Stimulus**: `"\U00020000 Hello"` (a chunk whose only letter is a supplementary-plane ideograph,
a surrogate pair) with an annotation over it and a break at offset 2; `"\U00020000. Hello"` with
the annotation but no breaks; the same text through `Chunk` with neither; and `"Hello \U0001F600"`
(an emoji, not a letter) with a break at offset 6.

**Expected**: With breaks or annotations supplied (the analyzed path) the ideograph chunk is kept
and keeps its annotation (`["\U00020000", "Hello"]`, and `["\U00020000.", "Hello"]` for the
annotation-only case); with neither supplied the legacy per-code-unit test still treats the chunk
as word-less, so `Chunk` returns `["Hello"]` exactly as before (the no-analyzer output is
byte-for-byte unchanged); the emoji chunk is still merged into the preceding chunk because it is
not a letter or digit.

**Tests**: `SentenceChunker_ChunkAligned_SurrogatePairLetterChunkWithBreaks_RetainedWithAnnotation`,
`SentenceChunker_ChunkAligned_SurrogatePairLetterChunkWithAnnotationsOnly_Retained`,
`SentenceChunker_Chunk_SurrogatePairLetterChunkNoAnalysis_KeepsLegacyFiltering`,
`SentenceChunker_ChunkAligned_SurrogatePairEmoji_StillDegenerate`

##### Annotated Punctuation-Only Pieces Are Kept

**Stimulus**: On the analyzed path: `"&"` with a non-empty-valued annotation over it; `"©; Brand"`
with a non-empty-valued annotation over the symbol, with `null` and with empty breaks; `"Hello. &"`
with the annotation on the ampersand; `"&, ,"` with the annotation on the ampersand;
`"Hello, \u200B, there"` with an *empty-valued* annotation on the invisible character; and
`"Wait , , stop."` with a non-empty annotation that crosses the two commas. Without analysis:
`Chunk("&")`, `ChunkWithMetadata("&")` and `ChunkAligned("&", ...)` with `null` or empty
annotations and `null` or empty breaks.

**Expected**: The annotated symbol is kept as its own chunk with its annotation (`["&"]`,
`["©;", "Brand"]`, `["Hello.", "&"]`); a following unannotated punctuation-only piece merges onto
it (`["&, ,"]`); an empty-valued annotation or a crossing annotation does not keep a piece
(`["Hello, \u200B,", "there"]` and `["Wait , ,", "stop."]`, the seam annotation re-based); and with
no annotations every no-analysis call still returns no chunks for `"&"`.

**Tests**: `SentenceChunker_ChunkAligned_AnnotatedSymbolOnlyText_IsKept`,
`SentenceChunker_ChunkAligned_AnnotatedSymbolBeforeClausePunctuation_KeepsChunkAndAnnotation`,
`SentenceChunker_ChunkAligned_AnnotatedSymbolAfterSentence_IsNotMergedIntoPreviousChunk`,
`SentenceChunker_ChunkAligned_UnannotatedPunctuationAfterAnnotatedSymbol_MergesOntoIt`,
`SentenceChunker_ChunkAligned_EmptyValueAnnotationOnPunctuation_StillMergedOrDropped`,
`SentenceChunker_ChunkAligned_AnnotationCrossingPunctuationBoundary_DoesNotKeepChunk`,
`SentenceChunker_Chunk_SymbolOnlyNoAnalysis_IsStillDropped`

##### No-Analyzer Differential Check Against `main`

**Stimulus**: The unchanged-path guarantee is proven by a differential run, repeated for every
change to the chunker: the `main` sources of `SentenceChunker`, `SegmentAnnotation` and
`AnnotationList` are built as a separate assembly (from `git archive`, in a temporary folder
outside the repository) and compared with the working-tree assembly under extern aliases over
300,000 random hostile texts (ellipses, spaced dots, symbols, invisible characters,
supplementary-plane letters, emoji, lone surrogates, numerals) at budgets 200, 1, 5, 12 and 40,
with random sentence breaks (including `null`).

**Expected**: Mode A (`Chunk`, `ChunkWithMetadata` and `ChunkAligned` with `null` or empty
annotations; 9,000,000 comparisons) and Mode B (only empty-valued annotations; 1,500,000
comparisons) show **0 differences** in chunk text, ellipsis flag and re-based annotations. Mode C
(mixed values; 1,500,000 runs) shows 0 invariant violations (every chunk non-empty and trimmed,
every letter-less chunk carries a non-empty-valued annotation, every annotation in range); 177,779
outputs differ from `main`, all of them annotated symbol chunks. The same result was recorded for
the P2-2 review-fix change (see the agent log `developer-text-en-p2-2-fixes-*`).

**Tests**: `SentenceChunker_Chunk_RepresentativeCorpus_MatchesGoldenChunks`,
`SentenceChunker_ChunkAligned_NoAnnotationsNoBreaks_EqualsChunkWithMetadata`,
`SentenceChunker_Chunk_SymbolOnlyNoAnalysis_IsStillDropped` (the differential run itself is a
throwaway tool, not a repository test)

##### Public Surface and Argument Validation

**Stimulus**: `ChunkAligned` is called with null text and a non-positive maximum length, and a
default-constructed `SentenceChunk` is read.

**Expected**: `ArgumentNullException` and `ArgumentOutOfRangeException`; the default struct reports
an empty annotation list rather than failing.

**Tests**: `SentenceChunker_ChunkAligned_NullText_ThrowsArgumentNullException`,
`SentenceChunker_ChunkAligned_NonPositiveMaxLength_ThrowsArgumentOutOfRangeException`,
`SentenceChunk_Annotations_DefaultStruct_IsEmpty`
