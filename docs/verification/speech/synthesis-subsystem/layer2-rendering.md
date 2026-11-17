### Layer2Rendering (IModelCapabilityProfile and DefaultModelCapabilityProfile)

#### Verification Approach

`DefaultModelCapabilityProfile` is a pure function from a Layer 1 span list and a model to a
`SpeechPlan`, so it is verified directly with a `StubSpeechModel` test double whose audio-tag
support level and parameters are set per test. Each test builds the span list by hand (or via
`AudioTagParser.Parse`) and asserts on the resulting segments' text, silence durations and
parameter overrides.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: None - no model files, no native runtime, no audio hardware
- **Test doubles**: `StubSpeechModel`

#### Acceptance Criteria

The unit is considered verified when a pause tag always yields a silence segment (long strictly longer than short) for
every support level; a native model receives the canonical bracket text; a parameter-mapped model gets a speed/volume
override for a mapped tag and a silent strip for an unmapped one; a `None` model has every non-pause tag stripped; plain
text is chunked into one segment per chunk; a chunk ending in an ellipsis gets a longer post-silence than an ordinary
sentence end; a parameter-mapped tag mid-narration overrides only the text after it; a plan or segment rejects null
content and negative silence; and a null span list or model is rejected. Annotation and break handling is verified when
span annotations are delivered on the segment containing them with offsets relative to that segment's text across
stripped, passed-through, pause, and parameter-mapped tags; a segment rejects a null, null-element, or out-of-range
annotation list and cannot be shortened beneath an annotation; span sentence breaks drive chunking, including at a tag
seam and between consecutive tags (carried by the tag span in every tag-support mode); a span list with no annotations
renders to exactly the pre-annotation segments; and the public `Instance`, `RenderText`, and pause constants are usable
and carry their documented values.

#### Test Scenarios

All tests are in `test/DemaConsulting.Speech.Tests/SynthesisSubsystem/DefaultModelCapabilityProfileTests.cs`.

##### Pauses Always Render As Silence

**Stimulus**: A short and a long pause tag are rendered for models of every support level.

**Expected**: A pure-silence segment, with the long pause strictly longer.

**Tests**: `DefaultModelCapabilityProfile_Render_ShortPause_AlwaysRendersAsSilenceSegment`,
`DefaultModelCapabilityProfile_Render_LongPause_RendersLongerSilenceThanShortPause`

##### Native, Parameter-Mapped, and Stripped Tags

**Stimulus**: Tags are rendered against `Native`, `ParameterMapped` and `None` models.

**Expected**: Native passes the canonical bracket text through; parameter-mapped maps `[fast]` to
a speed override and strips an emotion tag without an override; `None` strips every non-pause tag.

**Tests**: `DefaultModelCapabilityProfile_Render_NativeSupport_PassesThroughCanonicalBracketText`,
`DefaultModelCapabilityProfile_Render_ParameterMappedSupport_FastTag_MapsToSpeedParameter`,
`DefaultModelCapabilityProfile_Render_ParameterMappedSupport_EmotionTag_StripsWithNoOverride`,
`DefaultModelCapabilityProfile_Render_NoneSupport_StripsAllTags`

##### Parameter-Mapped Override Scope

**Stimulus**: `Hello [fast] world.` and consecutive mapped tags are rendered for a parameter-mapped model.

**Expected**: The override sits only on the segment after the tag; consecutive tags both apply to the next text.

**Tests**: `DefaultModelCapabilityProfile_Render_ParameterMappedTagMidText_OverridesOnlyFollowingText`,
`DefaultModelCapabilityProfile_Render_ConsecutiveParameterMappedTags_BothApplyToFollowingText`

##### Plan and Segment Validation

**Stimulus**: Plans and segments are constructed with null segments, null text, and negative silence.

**Expected**: Null list, null text, null element and negative silence are rejected; empty text with zero silence
is accepted; later changes to the caller list do not affect the plan.

**Tests**: `SpeechPlan_Constructor_NullSegments_ThrowsArgumentNullException`,
`SpeechPlan_Constructor_NullSegmentElement_ThrowsArgumentException`,
`SpeechPlan_Constructor_CallerListChangedLater_PlanIsUnaffected`,
`SpeechSegment_Constructor_NullText_ThrowsArgumentNullException`,
`SpeechSegment_Constructor_NegativeSilence_ThrowsArgumentOutOfRangeException`,
`SpeechSegment_Constructor_EmptyTextAndZeroSilence_IsAccepted`

##### Plain Text Chunking and Ellipsis Pause

**Stimulus**: Multi-sentence plain text, and text with a trailing adjacent or spaced ellipsis.

**Expected**: One segment per chunk; an ellipsis-ending chunk carries the longer post-silence.

**Tests**: `DefaultModelCapabilityProfile_Render_PlainTextOnly_RendersChunkedSegments`,
`DefaultModelCapabilityProfile_Render_ChunkEndingInEllipsis_SetsLongerPostSilenceThanOrdinarySentenceEnd`,
`DefaultModelCapabilityProfile_Render_ChunkEndingInSpacedEllipsis_AlsoSetsEllipsisPostSilence`,
`DefaultModelCapabilityProfile_Render_ChunkEndingInEllipsis_PauseDiffersFromShortAndLongTagPauses`

The ellipsis pause is distinct from both tag-pause durations.

##### Null Argument Rejection

**Stimulus**: `Render` is called with a null span list, then with a null model.

**Expected**: `ArgumentNullException` in both cases.

**Test**: `DefaultModelCapabilityProfile_Render_NullArguments_ThrowsArgumentNullException`

##### Annotations Are Re-Based Into Segments

**Stimulus**: Spans carrying annotations are rendered for `None`, `Native`, and `ParameterMapped`
models: an annotated span on its own; an annotated span after a stripped tag; an annotated span
after a native tag whose bracket text is passed through; and annotated spans on both sides of a
parameter-mapped tag and of a pause tag.

**Expected**: Each segment carries the annotations of its own text, with offsets relative to the
segment text (`Substring(Start, Length)` equals the annotated word). A stripped tag leaves later
offsets unchanged apart from the merged buffer, a native tag's passed-through text shifts later
offsets past itself, and a pause or parameter-mapped tag flushes the buffer so annotations never
cross the segment boundary.

**Tests**: `DefaultModelCapabilityProfile_Render_AnnotatedSpan_SegmentCarriesRebasedAnnotation`,
`DefaultModelCapabilityProfile_Render_AnnotationAfterStrippedTag_RebasedIntoMergedBuffer`,
`DefaultModelCapabilityProfile_Render_NativeTag_AnnotationOffsetsShiftedPastInsertedTagText`,
`DefaultModelCapabilityProfile_Render_ParameterMappedTag_AnnotationsStayWithTheirOwnSegment`,
`DefaultModelCapabilityProfile_Render_PauseTag_AnnotationsStayWithTheirOwnSegment`

##### Segment Annotation Validation

**Stimulus**: A `SpeechSegment` is read with no annotations supplied, constructed with an
annotation lying outside its text and with a null element, given an annotation list the caller later
changes, and copied with `with` to shorter text that would leave an annotation outside.

**Expected**: The default is an empty list; an out-of-range annotation and a null element throw
`ArgumentException`; the segment keeps its own copy; a shortening `with` throws
`ArgumentException`, so a segment can never be shortened underneath an annotation.

**Tests**: `SpeechSegment_Annotations_DefaultsToEmpty`,
`SpeechSegment_Annotations_OutOfRange_ThrowsArgumentException`,
`SpeechSegment_Annotations_NullElement_ThrowsArgumentException`,
`SpeechSegment_Annotations_CallerListChangedLater_SegmentIsUnaffected`,
`SpeechSegment_With_ShorterTextWouldOrphanAnnotation_ThrowsArgumentException`

##### Analyzer Breaks Drive Chunking

**Stimulus**: Spans carry authoritative sentence breaks that disagree with the punctuation; a break
lies exactly at the seam between two spans separated by a tag; a tag span carries a break (offset
`1`, after the tag; offset `0`, before it) between two consecutive tags under `None`, `Native` and
`ParameterMapped` support; and a control run has the same spans with no tag-span break.

**Expected**: Text splits at the supplied breaks rather than at punctuation, and a break at a tag
seam splits there. Under `None` the two plain texts become separate segments (`First`, `Second`);
under `Native` the split falls between the two tags' passed-through bracket text; offset `0` splits
immediately before the tag text; under `ParameterMapped` the result is the same two segments as
without the break (no artifacts); and the control, where the absence of a tag-span break means no
split, stays one segment (`FirstSecond`).

**Tests**: `DefaultModelCapabilityProfile_Render_SpanBreaksSupplied_SplitsAtAnalyzerBreaksNotPunctuation`,
`DefaultModelCapabilityProfile_Render_SentenceBreakAtTagSeam_SplitsAtSeam`,
`DefaultModelCapabilityProfile_Render_TagSpanBreakBetweenConsecutiveTags_NoneSupport_SplitsAtSeam`,
`DefaultModelCapabilityProfile_Render_TagSpanBreakBetweenConsecutiveTags_NativeSupport_SplitsBetweenTagTexts`,
`DefaultModelCapabilityProfile_Render_TagSpanBreakBetweenConsecutiveTags_ParameterMappedSupport_TwoSegments`,
`DefaultModelCapabilityProfile_Render_TagSpanBreakBeforeOffsetZero_SplitsBeforeTag`,
`DefaultModelCapabilityProfile_Render_AuthoritativeEmptyBreaksAcrossTagRun_StaysOneSegment`

##### Unannotated Output Unchanged

**Stimulus**: Spans without annotations or breaks are rendered, and the resulting segments are
compared with segments built the way they were before annotations existed.

**Expected**: The segments are equal to `new SpeechSegment(text, 0, 0, null)`-shaped values, so
rendering is unchanged when no analysis ran.

**Test**: `DefaultModelCapabilityProfile_Render_SpansWithoutAnnotations_SegmentsEqualPreChangeShape`

##### Public Default Profile, RenderText, and Constants

**Stimulus**: The public singleton is used from a test-local profile that wraps it; `RenderText`
is called with annotated text, with text ending in an ellipsis, and with null text; the public
pause constants are read.

**Expected**: The singleton is usable from outside the library; `RenderText` returns one segment
per chunk with re-based annotations, a chunk ending in an ellipsis gets
`EllipsisPauseMilliseconds` post-silence, and null text throws `ArgumentNullException`; the
constants are 300, 900, and 500 milliseconds.

**Tests**: `DefaultModelCapabilityProfile_Instance_IsPublicSingletonUsableByExternalProfile`,
`DefaultModelCapabilityProfile_RenderText_AnnotatedText_ReturnsSegmentsWithRebasedAnnotations`,
`DefaultModelCapabilityProfile_RenderText_EllipsisChunk_SetsEllipsisPostSilence`,
`DefaultModelCapabilityProfile_RenderText_NullText_ThrowsArgumentNullException`,
`DefaultModelCapabilityProfile_PauseConstants_HaveDocumentedValues`
