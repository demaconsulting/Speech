### AudioTagParser

#### Verification Approach

`AudioTagParser` and its `AudioTagCatalog` alias table are verified directly through pure unit
tests over plain strings; no test double is required since neither type depends on a model,
engine, audio device, or native runtime.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup: every test is a pure function call with no shared or persisted state.

#### Acceptance Criteria

This unit is considered verified when every canonical tag is reachable through at least one
unambiguous alias, alias resolution is case-insensitive and whitespace-tolerant, `Parse` produces
a correctly ordered span sequence for plain text, tags, and mixtures of the two (including edge
placement and adjacency), and every unrecognized or malformed bracket form passes through as
literal text rather than throwing or being discarded.

The unit is also verified when a catalog or parser caller cannot mutate the vocabulary (the tag
table and each alias list are read-only) and `Parse` rejects a null input.

#### Test Scenarios

The subsystem-level scenarios "Vocabulary: Completeness and Alias Resolution", "Vocabulary:
Normalization, Case-Insensitivity, and Whitespace Tolerance", "Vocabulary: Unresolvable Content",
"Parsing: Span Sequence and Ordering", and "Parsing: Malformed and Unrecognized Bracket
Passthrough" in the SynthesisSubsystem verification document cover this unit's catalog and
parser behavior in full. The named scenarios below are the unit's own.

##### AudioTagParser: Alias, Case, and Whitespace Resolution

**Tests**: `AudioTagParser_Parse_EveryDocumentedAlias_ResolvesToItsCanonicalTagSpan`,
`AudioTagParser_Parse_UppercaseTag_ResolvesToCanonicalTag`,
`AudioTagParser_Parse_TagWithExtraInternalWhitespace_ResolvesToCanonicalTag`

Verifies that every alias, in any case and with extra internal whitespace, parses to a single tag
span carrying the canonical tag.

##### AudioTagParser: Span Ordering and Boundary Placement

**Tests**: `AudioTagParser_Parse_PlainTextWithNoTags_ReturnsSinglePlainTextSpan`,
`AudioTagParser_Parse_EmptyInput_ReturnsEmptySpanList`,
`AudioTagParser_Parse_MixedPlainTextAndTag_ReturnsOrderedSpans`,
`AudioTagParser_Parse_AdjacentTags_ReturnsTwoSeparateTagSpans`,
`AudioTagParser_Parse_TagAtStartOfText_ReturnsTagSpanFirst`,
`AudioTagParser_Parse_TagAtEndOfText_ReturnsTagSpanLast`

Verifies the boundary cases (empty input, text only, tag at either edge, adjacent tags) produce
ordered spans with no spurious empty spans.

##### AudioTagParser: Malformed and Unrecognized Brackets

**Tests**: `AudioTagParser_Parse_UnknownBracketedWord_PassesThroughAsLiteralText`,
`AudioTagParser_Parse_UnclosedOpeningBracket_PassesThroughAsLiteralText`,
`AudioTagParser_Parse_EmptyBrackets_PassesThroughAsLiteralText`,
`AudioTagParser_Parse_StrayClosingBracket_PassesThroughAsLiteralText`,
`AudioTagParser_Parse_NestedOpeningBracketBeforeValidTag_ResolvesTag`,
`AudioTagParser_Parse_RecognizedTagFollowedByUnknownBracket_ReturnsTagThenLiteralSpans`

Verifies that nothing malformed is thrown on or dropped, and that a nested opening bracket does
not swallow a valid tag that follows it.

##### AudioTagParser: Null Input Rejection

**Test**: `AudioTagParser_Parse_NullText_ThrowsArgumentNullException`

Verifies the error path: null text throws `ArgumentNullException`, unlike empty text, which
returns an empty list.

##### AudioTagCatalog: Immutability, Kind Assignment, and Null Rejection

**Tests**: `AudioTagCatalog_Tags_Always_IsNotCastableToMutableList`,
`AudioTagCatalog_Tags_Always_AliasesAreNotCastableToMutableArray`,
`AudioTagCatalog_Tags_EveryCanonicalTag_HasExpectedKind`,
`AudioTagCatalog_NormalizeAndTryResolve_NullContent_ThrowsArgumentNullException`

Verifies the vocabulary cannot be mutated through its public collections, that each canonical tag
has its independently expected kind, and that null bracket content is rejected.
