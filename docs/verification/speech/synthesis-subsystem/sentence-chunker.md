### SentenceChunker

#### Verification Approach

`SentenceChunker` is a pure static function over strings, so it is verified directly with plain
string inputs and exact expected chunk lists; no test doubles are needed.

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
non-positive length or null text is rejected.

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

**Expected**: Numeral punctuation stays attached; each terminator run is one boundary.

**Tests**: `SentenceChunker_Chunk_ClausePunctuationEmbeddedInNumeral_StaysAttached`,
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
