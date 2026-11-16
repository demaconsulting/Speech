### TextAnalysis (ITextAnalyzer, TextAnalysisResult, SegmentAnnotation, TextAnalysisConventions)

#### Verification Approach

`SegmentAnnotation` and `TextAnalysisResult` are immutable value types and `TextAnalysisMapper` is
a pure function from a span list (and an analysis result) to a span list, so all three are verified
directly with plain strings, hand-built `TaggedTextSpan` lists, and exact expected offsets; no test
doubles are needed. `ITextAnalyzer` and `TextAnalysisConventions` carry no logic of their own: the
interface is exercised through the scripted analyzer double used by the `SynthesisSession` tests,
and the placeholder constant is verified through the analysis text the mapper builds. The
session-level behavior that wires the analyzer in - the once-per-request call, fault containment,
and cancellation - is verified in the SynthesisSession and SynthesisSubsystem verification
documents.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: None - no model files, no native runtime, no audio hardware
- **Test doubles**: None

#### Acceptance Criteria

The unit is considered verified when a `SegmentAnnotation` rejects a negative start, a non-positive
length, a null or blank kind and a null value (including through a `with` expression); a
`TextAnalysisResult` rejects a null annotation list, a null annotation and a negative break offset,
keeps its own copy, and sorts and de-duplicates its breaks; the analysis text keeps plain text and
contains exactly one placeholder per tag, in order; an annotation lying wholly inside one plain
span is attached to that span with span-local offsets; an annotation that covers a placeholder,
spans two plain spans, or lies beyond the text is dropped; sentence breaks are assigned to the
containing span with local offsets when supplied (every plain span receiving a possibly empty
list), a break between consecutive tags rides on the preceding tag span, and breaks are left
`null` when not supplied; and an empty analysis returns the same span list instance.

#### Test Scenarios

All tests are in `test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SegmentAnnotationTests.cs`,
`TextAnalysisResultTests.cs`, and `TextAnalysisMapperTests.cs`.

##### Annotation Validation

**Stimulus**: A `SegmentAnnotation` is constructed with a negative start, a zero or negative
length, a null or blank kind, and a null value, and an existing annotation is copied with an
invalid start through `with`.

**Expected**: `ArgumentOutOfRangeException` for the start and length, `ArgumentNullException` or
`ArgumentException` for the kind, `ArgumentNullException` for the value, and
`ArgumentOutOfRangeException` from the `with` expression, so an annotation can never be observed
in an invalid state.

**Tests** (`SegmentAnnotationTests.cs`):
`SegmentAnnotation_Constructor_NegativeStart_ThrowsArgumentOutOfRangeException`,
`SegmentAnnotation_Constructor_NonPositiveLength_ThrowsArgumentOutOfRangeException`,
`SegmentAnnotation_Constructor_BlankKind_Throws`,
`SegmentAnnotation_Constructor_NullValue_ThrowsArgumentNullException`,
`SegmentAnnotation_With_InvalidStart_ThrowsArgumentOutOfRangeException`

##### Result Validation and Defensive Copy

**Stimulus**: A `TextAnalysisResult` is constructed with a null annotation list, a null annotation
element, and a negative break offset; separately with lists the caller changes afterward, and with
unsorted, duplicated breaks.

**Expected**: The invalid arguments are rejected with `ArgumentNullException`,
`ArgumentException`, and `ArgumentOutOfRangeException` respectively; a later change to the
caller's lists does not affect the result; breaks are stored sorted and distinct.

**Tests** (`TextAnalysisResultTests.cs`):
`TextAnalysisResult_Constructor_NullAnnotations_ThrowsArgumentNullException`,
`TextAnalysisResult_Constructor_NullAnnotationElement_ThrowsArgumentException`,
`TextAnalysisResult_Constructor_NegativeBreak_ThrowsArgumentOutOfRangeException`,
`TextAnalysisResult_Constructor_CallerListsChangedLater_ResultIsUnaffected`,
`TextAnalysisResult_Constructor_UnsortedDuplicateBreaks_AreSortedAndDistinct`

##### Analysis Text Construction

**Stimulus**: `BuildAnalysisText` is called on a span list mixing plain and tag spans, and on a
list holding only plain text.

**Expected**: Each tag becomes exactly one `TextAnalysisConventions.TagPlaceholder`, plain text and
order are preserved, and a plain-only list yields exactly the original text.

**Tests** (`TextAnalysisMapperTests.cs`):
`TextAnalysisMapper_BuildAnalysisText_PlainAndTagSpans_UsesOnePlaceholderPerTag`,
`TextAnalysisMapper_BuildAnalysisText_PlainTextOnly_EqualsInput`

##### Span Annotation Assignment

**Stimulus**: An annotation lying wholly inside the second plain span of a three-span list, and a
list containing tag spans.

**Expected**: The annotation is attached to that span with a span-local offset (the offset of the
analysis text minus the span's start), and tag spans pass through as the same instances.

**Tests**: `TextAnalysisMapper_Annotate_AnnotationInsideSecondSpan_RebasedToSpanLocalOffset`,
`TextAnalysisMapper_Annotate_TagSpans_PassThroughUnchanged`

##### Unrepresentable Annotations Are Dropped

**Stimulus**: Annotations that cover a tag placeholder, that span two plain spans, and that end
beyond the length of the analysis text.

**Expected**: Each is dropped, never clipped or moved, and no exception is thrown.

**Tests**: `TextAnalysisMapper_Annotate_AnnotationCoveringPlaceholder_Dropped`,
`TextAnalysisMapper_Annotate_AnnotationSpanningTwoPlainSpans_Dropped`,
`TextAnalysisMapper_Annotate_AnnotationBeyondTextLength_Dropped`

##### Sentence Break Assignment

**Stimulus**: A break list whose entries fall inside plain spans, at span starts and ends, between
two consecutive tags, before a leading tag, after a trailing tag, and beyond the text; and a result
whose breaks are `null`.

**Expected**: Each break touching a plain span is assigned to it with a local offset; a break
strictly between two consecutive tags is attached to the tag span before it as tag offset `1`
(the following tag stays the same instance); a break after the last tag of a trailing run is
attached to that tag; a break before a leading tag or beyond the text is dropped; every plain span
receives a non-null, possibly empty list when breaks were supplied; spans keep `null` breaks when
none were supplied.

**Tests**: `TextAnalysisMapper_Annotate_BreaksProvided_AssignedToContainingSpansWithLocalOffsets`,
`TextAnalysisMapper_Annotate_BreakBetweenConsecutiveTags_AttachedAfterPrecedingTagSpan`,
`TextAnalysisMapper_Annotate_BreakBeforeLeadingTag_Dropped`,
`TextAnalysisMapper_Annotate_BreakAfterTrailingTag_AttachedToLastTag`,
`TextAnalysisMapper_Annotate_BreaksNull_SpansKeepNullBreaks`

##### Empty Result and Argument Validation

**Stimulus**: An empty analysis result is applied to a span list, and `BuildAnalysisText` and
`Annotate` are called with null arguments.

**Expected**: The same span list instance is returned for an empty result; null arguments throw.

**Tests**: `TextAnalysisMapper_Annotate_EmptyResult_ReturnsSameSpanListInstance`,
`TextAnalysisMapper_Annotate_NullArguments_Throw`
