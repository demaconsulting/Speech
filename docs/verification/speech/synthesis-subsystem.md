## SynthesisSubsystem Verification

### Verification Approach

The SynthesisSubsystem's Layer 1 scope - the closed Natural Language Audio Tag vocabulary and the
Layer 1 parser - is verified entirely through deterministic, pure unit tests over plain strings.
Neither `AudioTagCatalog` nor `AudioTagParser` depends on a model, an inference engine, an audio
device, or any native runtime, so no test double is required at this stage: every test calls the
production catalog/parser directly and asserts on their return values.

The Layer 3/Layer 5 Engine/Session API is verified the same way the RecognitionSubsystem's own
Engine/Session API is: a fake `ISynthesisBackend`/`ISynthesisBackendFactory` pair and an
NSubstitute `IAudioPlaybackDevice` stand in for the native synthesis backend and real speakers,
making Layer 2 rendering, chunking, per-operation sequential synthesize-then-play behavior, engine
exclusivity leasing, the session state machine, the overlap rule, cancellation, fault
containment, and honest degradation fully testable without a downloaded speech model or physical
audio hardware. Determinism is structural wherever practical: a deterministic fake backend plus
`SemaphoreSlim`-based test doubles let a test assert ordering, in-flight state, and fault
transitions without polling. No test relies on a bounded sleep: the drain-wait test awaits a
semaphore signaled by the stubbed device, abandonment is awaited through `StateChanged` reaching
`Faulted`, and "not yet complete" assertions are made against backends gated by the test, with
ordering witnesses captured at lease release.

The lease/exclusivity behavior is verified by driving `SpeechSynthesizerEngine`'s real
`CreateSessionAsync`/`DisposeAsync` logic against a fake backend, asserting that a second
concurrent session request fails fast with `SynthesisEngineBusyException` rather than hanging or
queueing, and that disposing a leased session (or the engine itself, with a session still active)
releases the lease so a subsequent request succeeds. The overlap rule and the full
`SynthesisSessionState` transition sequence are verified against the real
`SynthesisSession`, asserting the documented `Created → Starting → Running → Stopping →
Stopped` sequence for one successful operation (and the `Faulted` terminal transition for a
failing one) via the `StateChanged` event, and that a second call made while an operation is
already in flight on the same session throws `InvalidOperationException` immediately. The
`SynthesizeAsync` full-fidelity segment list is verified by asserting its returned
`IReadOnlyList<SynthesizedSpeech>` includes a pure-silence segment for a rendered pause tag, not
only the segments containing synthesized speech.

The text-analysis seam is verified in three layers. `SegmentAnnotation`, `TextAnalysisResult`, and
the internal `TextAnalysisMapper` are pure value types and functions verified directly with hand-built
span lists and exact expected offsets. `SentenceChunker.ChunkAligned` and
`DefaultModelCapabilityProfile` are verified with exact expected chunk and segment lists, and the
chunker's refactor onto a single position-aware implementation is proven behavior-preserving by a
golden corpus whose expected values were recorded from the previous implementation before it was
changed. The session wiring is verified with a scripted `FakeTextAnalyzer` and a
`FakeAnnotatedSynthesisEngine` (a backend that implements `IAnnotatedSynthesisBackend`), asserting
the analyzer's call count and received text, the annotation offsets each backend call received
(`text.Substring(Start, Length)` equals the annotated word), which of `Generate` and
`GenerateAnnotated` was called, and, through an NSubstitute diagnostics sink, that a failing
analyzer produces exactly one warning containing neither the exception message nor the text. Three
system-level scenarios in the system verification document exercise the same path through the
public factory.

Automated coverage **does not** include synthesizing real, intelligible speech. Proving that real
text produces correct audible speech through a real model on real speakers requires a downloaded
production model (which this library deliberately does not ship by default) and audio hardware,
so it remains a manual/local verification activity, mirroring the RecognitionSubsystem's
identical boundary. The real, native-backed backends ship in sibling systems (for example the
sherpa-onnx backend in the SpeechSherpa system); see _SpeechSherpa SynthesisSubsystem
Verification_.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: None - no external services, no downloaded model, no native runtime, no audio
  hardware
- **Test doubles**: None for the vocabulary/parser tests. For the Engine/Session pipeline: a fake
  `ISynthesisBackend`/`ISynthesisBackendFactory` pair, NSubstitute playback devices (including a
  semaphore-signaling `PendingSampleCount` stub for the genuine-drain-wait test) and diagnostics
  sinks, a fake synthesis model (optionally declaring a text analyzer and a custom backend), a
  scripted fake text analyzer, and a fake annotation-aware backend
- **Isolation**: Every vocabulary/parser test is a pure function call with no shared or persisted
  state; composition tests create and delete their own scratch installed-model directory

### Acceptance Criteria

A SynthesisSubsystem test run passes when:

- Every canonical `NaturalLanguageAudioTag` value has at least one alias in `AudioTagCatalog`,
  and no alias is shared by two different tags
- Every documented alias resolves back to its own canonical tag and kind
- Alias resolution is case-insensitive and tolerant of extra internal whitespace, and null
  bracket content or null parser input is rejected with `ArgumentNullException`
- The catalog's tag table and each descriptor's alias list are read-only (not castable back to a
  mutable collection), and every canonical tag has its independently expected kind
- `AudioTagParser.Parse` returns literal text and recognized tags as an ordered sequence of
  spans that reconstructs the input's structure, for plain text alone, a tag alone, adjacent
  tags, and a tag at the start or end of the input, with no spurious empty spans
- An empty input returns an empty span list
- Unrecognized bracket content, empty brackets, unclosed brackets, and stray closing brackets are
  passed through as literal text, exactly as they appeared, rather than throwing or being
  discarded, while a nested opening bracket never swallows a valid tag that follows it
- Pause tags always render as timed silence regardless of a model's declared audio-tag support;
  native support passes canonical bracket text through unchanged; parameter-mapped support maps
  the handful of tags with a numeric convention and strips every other tag; no support strips
  every non-pause tag; plain text is split into chunk-sized segments
- `SentenceChunker.Chunk` splits on primary sentence-ending punctuation first, then
  unconditionally splits every resulting sentence-level piece further on secondary clause
  punctuation, falls back to a whitespace-budget split only when still over budget, merges
  degenerate word-less pieces onto the preceding chunk, never splits a single word, and rejects a
  non-positive budget or null text, and yields no chunks for empty or whitespace-only text.
  `ChunkWithMetadata` additionally flags, per chunk, whether the
  chunk's text ends in a genuine ellipsis
- `SpeechSynthesizerFactory.LoadAsync` returns a real engine only when the model is installed,
  declares the synthesis role, and the backend loads; every other outcome returns the honest
  unavailable engine without throwing
- A supplied `parameterValues` key naming a parameter not declared by the requested model is
  silently ignored (with only an `Info` diagnostic reported) and composition still succeeds; a
  supplied value for a parameter the model _does_ declare that fails that parameter's own
  validation throws `ArgumentException` synchronously from `LoadAsync()`, before any
  installed/role/backend-load check runs
- `ISpeechSynthesizerEngine.CreateSessionAsync` succeeds when no lease is held and fails fast with
  `SynthesisEngineBusyException` when one is; disposing a leased session or the engine releases
  the lease so a subsequent request succeeds again; a `CreateSessionAsync` racing `DisposeAsync`
  never observes a torn state; and a `LoadAsync` canceled after the backend has been created still
  disposes that backend
- The engine's one-shot `SpeakAsync` creates and disposes a session per call, and its one-shot
  `SynthesizeAsync` returns segments without a playback device
- `SynthesisEngineBusyException` and `SynthesisSessionFaultedException` follow the standard
  constructor pattern, preserving the supplied message and inner exception
- A session's `SpeakAsync`/`SynthesizeAsync` calls never overlap on the same instance - a second
  call while one is in flight throws `InvalidOperationException` - and the session can be reused
  across many sequential calls without reconstruction
- A session's `StateChanged` event reports the documented `Created → Starting → Running →
  Stopping → Stopped` sequence for one successful operation, and transitions to the terminal
  `Faulted` state (reported through diagnostics) for a failing one; every subsequent operation on
  a faulted session throws `SynthesisSessionFaultedException` wrapping the original fault
- `SynthesizeAsync` returns the full, ordered `IReadOnlyList<SynthesizedSpeech>` segment list,
  including a pure-silence segment for a rendered pause, without requiring a playback device
- Text flows through chunking, rendering, and the backend to yield ordered audio segments, played
  in order with correct pre/post silence
- A long, multi-sentence input still yields segments in the correct order, and the backend is
  never called concurrently
- `StopAsync` cancels an in-flight operation deterministically and is a safe no-op when idle;
  backend faults and an unavailable playback device fail the caller's task honestly rather than
  hanging
- A session-level `parameterValues` bag resolves to the correct speaker id via
  `ISynthesisModel.ResolveSpeakerId` once per segment, coexisting correctly with an independent
  per-segment Natural Language Audio Tag speed override in the same call
- `DedicatedWorker` completes promptly on cooperative cancellation, and abandons (reporting a
  diagnostic and still returning control to its caller) a non-cooperative delegate after its
  timeout, always running the delegate on a long-running task
- `PlaybackAudioResampler` resamples and upmixes backend-rate mono audio to the playback device's
  resolved format
- The unavailable engine and unavailable session both stay honest and safe to hold and dispose
- The automated verification boundary remains honest about the absence of real-speech coverage
- A text analyzer is called exactly once per request with the whole normalized text in which each
  recognized tag is one placeholder; its annotations reach an annotation-accepting backend with
  offsets relative to each segment's text across stripped tags, native tags, and chunk boundaries,
  and an annotation that cannot be represented inside one segment is dropped rather than misplaced
- Analyzer-supplied sentence breaks replace punctuation-based sentence splitting, while the
  clause and budget passes still apply, including a break between two consecutive tags
- A slow text analyzer runs on a thread-pool thread and does not delay the return of the speak or
  synthesize call that started it
- A chunk whose only letters are supplementary-plane characters is kept with its annotation on
  the analyzed path, while text with no analysis keeps its legacy chunking exactly
- A segment with annotations reaches only a backend that accepts them, through `GenerateAnnotated`;
  a legacy backend, and a segment without annotations, always receive plain `Generate` calls with
  text identical to a run with no analysis
- With no analyzer, a `null` result, or an empty result, a request behaves exactly as before text
  analysis existed, and the sentence chunker's output for unannotated text matches the golden corpus
  recorded from the pre-refactor implementation
- A failing analyzer never faults a session and is reported through one warning that names only
  the exception type; an analyzer cancellation propagates as `OperationCanceledException`
- The default rendering profile, its `RenderText` step and pause constants, and the sentence
  chunker are public and usable by an external model profile

### Test Scenarios

#### Vocabulary: Completeness and Alias Resolution

**Tests**: `AudioTagCatalog_Tags_Always_CoversEveryCanonicalTagWithAtLeastOneAlias`,
`AudioTagCatalog_Tags_Always_NoAliasIsSharedByTwoDifferentTags`,
`AudioTagCatalog_TryResolve_EveryDocumentedAlias_ResolvesToItsOwnTag`,
`AudioTagCatalog_TryResolve_MoodTagsAndShoutAlias_ResolveToExpectedTag`,
`AudioTagCatalog_Tags_Always_IsNotCastableToMutableList`,
`AudioTagCatalog_Tags_Always_AliasesAreNotCastableToMutableArray`,
`AudioTagCatalog_Tags_EveryCanonicalTag_HasExpectedKind`

The catalog includes nine mood tags (`angry`, `calm`, `happy`, `enthused`, `surprised`, `worried`,
`confused`, `frustrated`, `neutral`), all of kind Emotion, plus the `shout` alias of `Loud`.

Verifies that every declared `NaturalLanguageAudioTag` value is reachable through at least one
alias, that no alias is ambiguous between two tags, and that every documented alias resolves
back to its own canonical tag and kind, sourced directly from `AudioTagCatalog.Tags` so this
coverage can never drift out of sync with the catalog it exercises. Also verifies that each
canonical tag has its independently expected kind, and that neither the tag table nor any
descriptor's alias list can be cast back to a mutable collection.

#### Vocabulary: Normalization, Case-Insensitivity, and Whitespace Tolerance

**Tests**: `AudioTagCatalog_Normalize_VariousInputs_ProducesExpectedNormalForm`,
`AudioTagCatalog_TryResolve_MixedCaseWithExtraWhitespace_ResolvesToCanonicalTag`,
`AudioTagParser_Parse_UppercaseTag_ResolvesToCanonicalTag`,
`AudioTagParser_Parse_TagWithExtraInternalWhitespace_ResolvesToCanonicalTag`

Verifies that normalization trims, lower-cases, and collapses internal whitespace, and that both
the catalog and the parser resolve mixed-case, extra-whitespace tag text to the correct
canonical tag and kind.

#### Vocabulary: Unresolvable Content

**Tests**: `AudioTagCatalog_TryResolve_UnknownWord_ReturnsFalse`,
`AudioTagCatalog_TryResolve_EmptyOrWhitespace_ReturnsFalse`,
`AudioTagCatalog_NormalizeAndTryResolve_NullContent_ThrowsArgumentNullException`

Verifies that an unknown word and empty or whitespace-only bracket content never resolve to a
tag, and that null content is rejected with `ArgumentNullException` by both `Normalize` and
`TryResolve`.

#### Parsing: Span Sequence and Ordering

**Tests**: `AudioTagParser_Parse_EveryDocumentedAlias_ResolvesToItsCanonicalTagSpan`,
`AudioTagParser_Parse_PlainTextWithNoTags_ReturnsSinglePlainTextSpan`,
`AudioTagParser_Parse_EmptyInput_ReturnsEmptySpanList`,
`AudioTagParser_Parse_MixedPlainTextAndTag_ReturnsOrderedSpans`,
`AudioTagParser_Parse_AdjacentTags_ReturnsTwoSeparateTagSpans`,
`AudioTagParser_Parse_TagAtStartOfText_ReturnsTagSpanFirst`,
`AudioTagParser_Parse_TagAtEndOfText_ReturnsTagSpanLast`,
`AudioTagParser_Parse_NullText_ThrowsArgumentNullException`

Verifies that every documented alias, bracketed alone, resolves to a single tag span; that plain
text with no tags, and an empty input, each produce the expected trivial span list; that null
text is rejected with `ArgumentNullException`; and that
mixed text, adjacent tags, and tags at either edge of the input all produce correctly ordered
spans with no spurious empty spans.

#### Parsing: Malformed and Unrecognized Bracket Passthrough

**Tests**: `AudioTagParser_Parse_UnknownBracketedWord_PassesThroughAsLiteralText`,
`AudioTagParser_Parse_UnclosedOpeningBracket_PassesThroughAsLiteralText`,
`AudioTagParser_Parse_EmptyBrackets_PassesThroughAsLiteralText`,
`AudioTagParser_Parse_StrayClosingBracket_PassesThroughAsLiteralText`,
`AudioTagParser_Parse_RecognizedTagFollowedByUnknownBracket_ReturnsTagThenLiteralSpans`,
`AudioTagParser_Parse_NestedOpeningBracketBeforeValidTag_ResolvesTag`

Verifies that an unrecognized bracketed word, an unclosed opening bracket, empty brackets, and a
stray closing bracket all pass through as literal text exactly as they appeared; that a nested
opening bracket in front of a valid tag does not swallow the tag; and that a
recognized tag immediately followed by an unrecognized bracket yields distinct, correctly
ordered tag and literal spans rather than merging or losing either.

#### Layer 2 Rendering: Pauses, Native, Parameter-Mapped, and Stripped Tags

**Tests**: `DefaultModelCapabilityProfile_Render_ShortPause_AlwaysRendersAsSilenceSegment`,
`DefaultModelCapabilityProfile_Render_LongPause_RendersLongerSilenceThanShortPause`,
`DefaultModelCapabilityProfile_Render_NativeSupport_PassesThroughCanonicalBracketText`,
`DefaultModelCapabilityProfile_Render_ParameterMappedSupport_FastTag_MapsToSpeedParameter`,
`DefaultModelCapabilityProfile_Render_ParameterMappedSupport_EmotionTag_StripsWithNoOverride`,
`DefaultModelCapabilityProfile_Render_ParameterMappedTagMidText_OverridesOnlyFollowingText`,
`DefaultModelCapabilityProfile_Render_ConsecutiveParameterMappedTags_BothApplyToFollowingText`,
`SpeechPlan_Constructor_NullSegments_ThrowsArgumentNullException`,
`SpeechPlan_Constructor_NullSegmentElement_ThrowsArgumentException`,
`SpeechPlan_Constructor_CallerListChangedLater_PlanIsUnaffected`,
`SpeechSegment_Constructor_NullText_ThrowsArgumentNullException`,
`SpeechSegment_Constructor_NegativeSilence_ThrowsArgumentOutOfRangeException`,
`SpeechSegment_Constructor_EmptyTextAndZeroSilence_IsAccepted`,
`DefaultModelCapabilityProfile_Render_NoneSupport_StripsAllTags`,
`DefaultModelCapabilityProfile_Render_NoneSupport_MoodTag_StripsAndSpeaksWords`,
`DefaultModelCapabilityProfile_Render_NativeSupport_MoodTag_PassesCanonicalText`,
`DefaultModelCapabilityProfile_Render_PlainTextOnly_RendersChunkedSegments`,
`DefaultModelCapabilityProfile_Render_NullArguments_ThrowsArgumentNullException`,
`DefaultModelCapabilityProfile_Render_ChunkEndingInEllipsis_SetsLongerPostSilenceThanOrdinarySentenceEnd`,
`DefaultModelCapabilityProfile_Render_ChunkEndingInSpacedEllipsis_AlsoSetsEllipsisPostSilence`

Verifies that pauses always render as silence regardless of declared support, that native
support passes tags through unchanged, that parameter-mapped support maps the tags with a
built-in numeric convention and strips every other tag, that no support strips every non-pause
tag, that plain text is split into chunked segments, and that null arguments are rejected. A
parameter-mapped tag in the middle of narration (`Hello [fast] world.`) is verified to put the
override only on the segment spoken after the tag, and consecutive tags both apply to the text
that follows them. A speech plan or segment is verified to reject missing content, reject a
negative silence, and keep its own copy of the segment list. Also
verifies that a chunk whose text ends in a genuine ellipsis (adjacent or whitespace-spaced)
renders a longer post-chunk silence (`EllipsisPauseMilliseconds`) than an ordinary
single-terminator sentence end, while every other chunk boundary still adds zero silence.

#### Chunking: Boundary Splitting and Edge Cases

**Tests**: `SentenceChunker_Chunk_ShortSingleSentence_ReturnsOneChunk`,
`SentenceChunker_Chunk_MultipleSentences_SplitsOnPrimaryBoundaries`,
`SentenceChunker_Chunk_MidSentenceEllipsis_StaysAttachedAsSingleChunk`,
`SentenceChunker_Chunk_MixedConsecutiveTerminators_StaysAttachedAsSingleChunk`,
`SentenceChunker_Chunk_RepeatedExclamations_StaysAttachedAsSingleChunk`,
`SentenceChunker_Chunk_SingleTerminators_NoRegression`,
`SentenceChunker_Chunk_TrailingEllipsis_NoDegenerateTrailingChunk`,
`SentenceChunker_Chunk_LongSentenceWithClauses_SplitsOnSecondaryBoundaries`,
`SentenceChunker_Chunk_LongClauseWithNoPunctuation_SplitsOnWhitespaceBudget`,
`SentenceChunker_Chunk_SingleWordExceedsBudget_ReturnsWholeWord`,
`SentenceChunker_Chunk_NonPositiveMaxLength_ThrowsArgumentOutOfRangeException`,
`SentenceChunker_Chunk_NullText_ThrowsArgumentNullException`,
`SentenceChunker_Chunk_ShortSentenceWithComma_SplitsOnCommaEvenUnderBudget`,
`SentenceChunker_Chunk_ShortSentenceWithSemicolonOrColon_SplitsEvenUnderBudget`,
`SentenceChunker_Chunk_LetterPrecededPeriodFollowedByDigit_Splits`,
`SentenceChunker_Chunk_ClausePunctuationEmbeddedInNumeral_StaysAttached`,
`SentenceChunker_Chunk_LeadingDecimalPoint_StaysAttached`,
`SentenceChunker_Chunk_SpacedPunctuationRun_MergesIntoPrecedingChunk`,
`SentenceChunker_Chunk_LoneSecondaryBoundaryWithNoWordContent_MergesIntoPrecedingChunk`,
`SentenceChunker_Chunk_LeadingDegenerateRun_IsDropped`,
`SentenceChunker_ChunkWithMetadata_TrailingAdjacentEllipsis_EndsWithEllipsisIsTrue`,
`SentenceChunker_ChunkWithMetadata_TrailingSpacedEllipsis_EndsWithEllipsisIsTrue`,
`SentenceChunker_ChunkWithMetadata_FewerThanThreeDots_EndsWithEllipsisIsFalse`,
`SentenceChunker_Chunk_AndChunkWithMetadata_ProduceSameChunkText`,
`SentenceChunker_Chunk_EmptyOrWhitespace_ReturnsEmpty`

Verifies that chunking splits on primary sentence-ending punctuation first, then
**unconditionally** splits every resulting sentence-level piece further on secondary clause
punctuation (commas, semicolons, colons) regardless of whether the piece is still over budget, so
every clause becomes its own chunk; falls back to a whitespace budget split only when a piece is
still over-length after both punctuation passes; never splits a single over-length word; and
rejects a non-positive budget or null text. Also verifies that clause punctuation flanked by a
digit on both sides is never treated as a boundary, so numerals stay intact, while a comma,
semicolon, or colon still splits normally when only one side is a digit. Also verifies that a
decimal point followed immediately by a digit is kept attached to its numeral even without a
leading digit, and that a maximal run of consecutive primary sentence-ending characters is treated
as a single boundary and stays attached to the preceding sentence as one piece. Also verifies that
a degenerate, word-less punctuation piece is merged onto the immediately preceding non-empty
chunk, or dropped entirely when it occurs at the very start of the text. Finally, verifies that
`ChunkWithMetadata` correctly flags a chunk as ending in a genuine ellipsis and not for fewer than
three dots, and that `Chunk` produces exactly the same chunk text as `ChunkWithMetadata`. Empty
or whitespace-only text yields no chunks.

#### Composition: Real Engine for an Installed Model

**Tests**: `SpeechSynthesizerFactory_LoadAsync_ModelInstalled_ReturnsRealEngine`,
`SpeechSynthesizerFactory_LoadAsync_WithStoreModelInstalled_ReturnsRealEngine`,
`SpeechSynthesizerFactory_LoadAsync_WithCatalogModelInstalled_ReturnsRealEngine`

Verifies that an installed synthesis model composes a real engine wired to the injected backend
factory, with the installed-model directory passed through unchanged, whether that directory is
supplied directly as a `string`, resolved from a `SpeechModelStore`, or resolved from a
`SpeechModelCatalog`'s own store.

#### Composition: Honest Fallback for Every Unavailable State

**Tests**: `SpeechSynthesizerFactory_LoadAsync_ModelNotInstalled_ReturnsUnavailableEngine`,
`SpeechSynthesizerFactory_LoadAsync_ModelRoleIsNotSynthesis_ReturnsUnavailableEngine`,
`SpeechSynthesizerFactory_LoadAsync_EngineLoadFails_ReturnsUnavailableEngineAndDoesNotThrow`,
`SpeechSynthesizerFactory_LoadAsync_WithStoreModelNotInstalled_ReturnsUnavailableEngine`,
`SpeechSynthesizerFactory_LoadAsync_WithCatalogModelNotInstalled_ReturnsUnavailableEngine`

Verifies that a missing model, a wrong-role model, and a failed backend load all degrade to the
shared unavailable engine without throwing.

#### Composition: Null Arguments and Cancellation Are Programming Errors

**Tests**: `SpeechSynthesizerFactory_LoadAsync_NullModel_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_LoadAsync_CanceledToken_ThrowsOperationCanceledException`,
`SpeechSynthesizerFactory_LoadAsync_WithStoreNullModel_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_LoadAsync_WithStoreNullStore_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_LoadAsync_WithCatalogNullModel_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_LoadAsync_WithCatalogNullCatalog_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_LoadAsync_CanceledDuringGraceWindowCreateSucceeds_ThrowsAndDisposesBackend`

Verifies that a null model, store, or catalog throws, and that a canceled `cancellationToken`
throws `OperationCanceledException` - including a cancellation that arrives after the backend
creation has already succeeded, in which case the created backend is disposed rather than
leaked - distinguishing a programming error/explicit cancellation
request from an ordinary machine state. Unlike the former synchronous factory, no
`NullPlaybackDevice` variant exists for any overload: `LoadAsync` no longer takes a playback
device parameter at all.

#### Composition: Parameter Value Forwarding and Validation

**Tests**: `SpeechSynthesizerFactory_LoadAsync_ParameterValuesSupplied_ForwardedToEngine`,
`SpeechSynthesizerFactory_LoadAsync_UnrecognizedParameterId_ComposesAndReportsInfo`,
`SpeechSynthesizerFactory_LoadAsync_RecognizedNumericParameterOutOfRange_Throws`,
`SpeechSynthesizerFactory_LoadAsync_RecognizedNumericParameterWrongType_Throws`,
`SpeechSynthesizerFactory_LoadAsync_RecognizedIntegerParameterFractionalValue_Throws`,
`SpeechSynthesizerFactory_LoadAsync_RecognizedChoiceParameterInvalidOption_Throws`,
`SpeechSynthesizerFactory_LoadAsync_RecognizedBooleanParameterWrongType_Throws`

Verifies that an optional `parameterValues` bag supplied by the caller is forwarded unchanged to
the constructed engine; that a supplied `parameterValues` key naming a parameter the model does
not declare still composes a real engine and reports only an `Info` diagnostic, never throwing
(preserving cross-model compatibility); and that a supplied value for a parameter the model _does_
declare, but that is invalid for it, throws `ArgumentException` synchronously from `LoadAsync()` -
before any installed/role/backend-load check runs - naming the parameter id, the model id, and the
specific reason the value is invalid.

#### Engine: Session Exclusivity and Lease Lifecycle

**Tests**: `SpeechSynthesizerEngine_CreateSessionAsync_NoLeaseHeld_ReturnsRealSession`,
`SpeechSynthesizerEngine_CreateSessionAsync_LeaseAlreadyHeld_ThrowsSynthesisEngineBusyException`,
`SpeechSynthesizerEngine_CreateSessionAsync_AfterPriorSessionDisposed_SucceedsAgain`,
`SpeechSynthesizerEngine_CreateSessionAsync_NullDevice_ThrowsArgumentNullException`,
`SpeechSynthesizerEngine_IsAvailable_Always_ReturnsTrue`,
`SpeechSynthesizerEngine_DisposeAsync_CalledTwice_DisposesBackendOnce`,
`SpeechSynthesizerEngine_DisposeAsync_WithActiveLeasedSession_DisposesSessionFirst`,
`SpeechSynthesizerEngine_CreateSessionAsync_RacingDisposeAsync_NeverObservesTornState`,
`SynthesisEngineBusyException_Constructors_PreserveMessageAndInnerException`,
`SynthesisSessionFaultedException_Constructors_PreserveMessageAndInnerException`

Verifies that `CreateSessionAsync` succeeds immediately when no lease is held; fails fast with
`SynthesisEngineBusyException` (never queueing or waiting) when a lease is already held by a
still-undisposed session; succeeds again once that prior session is disposed, proving the lease is
released exactly once from the session's own `DisposeAsync`; rejects a null device; always reports
itself available; disposes its owned backend exactly once across repeated `DisposeAsync` calls;
and, when disposed while a session is still actively leased, disposes that session first (which
also releases the lease) before disposing the backend. A `CreateSessionAsync` racing a concurrent
`DisposeAsync` either yields a usable session or fails cleanly with `ObjectDisposedException` (or
the lease-busy exception), and never any other exception indicating a half-disposed engine. The two
lease/fault exceptions each expose the message and inner exception supplied to their constructors.

#### Engine: One-Shot Convenience Overloads

**Tests**: `SpeechSynthesizerEngine_SpeakAsync_CalledTwice_CreatesAndDisposesASessionEachTime`,
`SpeechSynthesizerEngine_SynthesizeAsync_NoDeviceSupplied_ReturnsSegments`

Verifies that the engine's one-shot `SpeakAsync` convenience overload creates and disposes a
fresh session per call (so repeated calls never conflict with the exclusivity lease), and that
`SynthesizeAsync` returns synthesized segments without requiring a playback device at all.

#### Session: Lifecycle State Machine and StateChanged Event

**Tests**: `SynthesisSession_StateChanged_OneSuccessfulOperation_RaisesExpectedTransitionsInOrder`,
`SynthesisSession_StateChanged_HandlerThrows_IsIsolatedAndDoesNotPropagate`,
`SynthesisSession_StateChanged_FaultingOperation_RaisesTransitionsEndingInFaulted`,
`SynthesisSession_StateChanged_Dispose_RaisesDisposingThenDisposed`,
`SynthesisSession_IsAvailable_BeforeAndAfterDispose_ReflectsLifecycle`,
`SynthesisSession_IsAvailable_AfterFault_ReturnsFalse`,
`SynthesisSession_StateChanged_HandlerBlocksOnOtherThreadReadingState_DoesNotDeadlock`

Verifies that one successful `SpeakAsync`/`SynthesizeAsync` call raises the documented
`Created → Starting → Running → Stopping → Stopped` sequence via `StateChanged`, in order; that a
faulting operation raises `Created → Starting → Running → Faulted` directly (never passing through
`Stopping`); that a subscriber's handler exception
is caught and routed to diagnostics rather than propagated or destabilizing the session; that a
handler blocking while another thread reads `State` does not deadlock (handlers never run under
the session lock); that
disposal reports `Disposing` and then the terminal `Disposed`; and that
`IsAvailable` reports false once the session is disposed or faulted.

#### Session: Overlap Rule - No Concurrent Operations

**Tests**: `SynthesisSession_SpeakAsync_CalledWhileAlreadySpeaking_ThrowsInvalidOperationException`,
`SynthesisSession_SynthesizeAsync_CalledWhileAlreadySpeaking_ThrowsInvalidOperationException`

Verifies that `SpeakAsync`/`SynthesizeAsync` never overlap on the same session instance: a second
call of either method made while one is already in flight throws `InvalidOperationException`
immediately rather than queueing or waiting, for every combination of the two methods.

#### Session: Hot Reuse Across Repeated Calls

**Test**: `SynthesisSession_SpeakAsync_CalledTwiceOnSameInstance_ReusesSameInstanceWithoutReconstruction`

Verifies the key Engine/Session redesign goal: a session returns to `Stopped` after one operation
completes and can be reused for a second, sequential `SpeakAsync` call on the very same instance,
with no reconstruction - closing the class of bug where a host reconstructed a synthesizer per
utterance.

#### Session: Faulted Is Terminal

**Tests**: `SynthesisSession_SynthesizeAsync_BackendThrows_ReportsFaultAndTransitionsToFaulted`,
`SynthesisSession_SynthesizeAsync_AfterFault_ThrowsSynthesisSessionFaultedException`,
`SynthesisSessionFaultedException_Constructors_PreserveMessageAndInnerException`

Verifies that a non-cancellation failure during an operation reports the fault through
diagnostics and transitions the session to the terminal `Faulted` state, and that every
subsequent operation on that same session throws `SynthesisSessionFaultedException` wrapping the
original fault rather than attempting to run again.

#### Session: SynthesizeAsync Full-Fidelity Segment List

**Test**: `SynthesisSession_SynthesizeAsync_ReturnsFullFidelitySegmentListIncludingSilence`

Verifies that `SynthesizeAsync` returns the complete, ordered segment list the rendered
`SpeechPlan` produced, including a pure-silence `SynthesizedSpeech` segment for a rendered pause
tag - not only the segments containing synthesized speech - so a caller that saves or otherwise
processes the returned segments gets a faithful, lossless reconstruction of the plan.

#### Session: Chunked Synthesis and Ordered Playback

**Tests**: `SynthesisSession_SynthesizeAsync_PlainText_YieldsAudioSegment`,
`SynthesisSession_SpeakAsync_PlainText_StartsWritesAndStopsDevice`,
`SynthesisSession_SynthesizeAsync_LongMultiSentenceInput_ProducesOrderedSegmentsSequentially`,
`SynthesisSession_SpeakAsync_MultiSentenceInput_WritesEachSegmentBeforeSynthesizingNext`

Verifies that plain text yields a synthesized audio segment, that `SpeakAsync` starts the
playback device, writes segments in order, and stops the device, and that a long, multi-sentence
input still yields segments in the correct order with the backend never called concurrently.
Synthesis is sequential: each segment is synthesized and written before the next is synthesized
(a backend fake records, at each `Generate` call, how many segments have already been written);
no scenario expects synthesis to overlap playback.

#### Session: Fault Containment

**Tests**: `SynthesisSession_SpeakAsync_PlaybackDeviceWriteThrows_PropagatesAndStillStopsDevice`,
`SynthesisSession_SpeakAsync_PlaybackDeviceUnavailable_ThrowsRatherThanHanging`,
`SynthesisSession_SpeakAsync_PlaybackDeviceStartThrows_StillCallsStop`

Verifies that a playback write failure still stops the device before propagating, that an
unavailable playback device fails promptly rather than hanging, and that a failure to start the
device still results in a stop attempt during teardown.

#### Session: Genuine Playback Drain Before Stopping

**Tests**: `SynthesisSession_SpeakAsync_PlaybackDeviceReportsPendingSamples_WaitsForDrainBeforeStopping`,
`SynthesisSession_SpeakAsync_CanceledDuringDrainWait_ExitsWaitPromptly`

Verifies that `SpeakAsync` genuinely waits for the playback device to report a drained queue
before stopping it, rather than stopping as soon as every segment has been written, and that
canceling during that wait ends it promptly instead of waiting for the queue to drain. Uses an
NSubstitute playback device whose `PendingSampleCount` getter signals a semaphore on every read,
so the test deterministically observes the wait has genuinely begun (rather than racing a sleep)
before asserting the awaited task has not completed and `Stop()` has not been called; only after
the test sets the reported pending count to zero does the task complete and the device get
stopped.

#### Session: Cancellation and Disposal Lifecycle

**Tests**: `SynthesisSession_StopAsync_WhileSpeaking_DoesNotCompleteUntilInFlightGenerateReturns`,
`SynthesisSession_StopAsync_NoOperationInFlight_IsNoOp`,
`SynthesisSession_DisposeAsync_CalledTwice_ReleasesLeaseOnce`,
`SynthesisSession_SpeakAsync_AfterDispose_ThrowsObjectDisposedException`,
`SynthesisSession_SynthesizeAsync_AfterDispose_ThrowsObjectDisposedException`

Verifies that `StopAsync` cancels an in-flight operation and does not complete until the
in-flight `DedicatedWorker`-routed `Generate` call has genuinely returned (never orphaning it); is
a safe no-op when no operation is in flight; releases the engine's exclusivity lease exactly once
across repeated `DisposeAsync` calls; and that both `SpeakAsync` and `SynthesizeAsync` on a disposed
session throw `ObjectDisposedException`.

#### Session: Abandoned Native Calls Fault the Session

**Tests**: `SynthesisSession_AbandonedGenerate_FaultsSessionAndRejectsReuseImmediately`,
`SynthesisSession_SpeakAsync_AbandonedGenerate_ReturnsCanceledOperationWhileNativeCallStillRunning`,
`SynthesisSession_AbandonedGenerate_ReportsFaultThroughDiagnostics`,
`SynthesisSession_StopAsync_AbandonedGenerate_DoesNotCompleteUntilNativeCallGenuinelyReturns`,
`SynthesisSession_DisposeAsync_CalledAfterAbandonmentFault_DoesNotReleaseLeaseUntilNativeCallGenuinelyReturns`,
`SynthesisSession_DisposeAsync_AbandonedWorker_DoesNotReleaseLeaseUntilWorkerExits`,
`SynthesisSession_StopAsync_AbandonedDeviceStart_DoesNotCallDeviceStopUntilStartGenuinelyReturns`,
`SynthesisSession_StopAsync_CallerTokenPreCanceled_ThrowsButOperationStillStops`,
`SynthesisSession_StopAsync_CallerTokenPreCanceled_OperationStillCanceledAndAwaited`,
`SynthesisSession_SpeakAsync_SlowPlaybackDeviceStart_DoesNotBlockCaller`,
`SynthesisSession_DisposeAsync_WhileSpeaking_DoesNotReleaseLeaseBeforeOperationSettles`,
`SynthesisSession_DisposeAsync_RacingSpeakAsync_NeverDisposesBackendWhileOperationInFlight`,
`SynthesisSession_DisposeAsync_CalledConcurrentlyTwice_BothCompleteAfterSingleTeardown`,
`SynthesisSession_DisposeAsync_AfterRejectedOverlappingCall_StillAwaitsOriginalOperation`,
`SynthesisSession_StopAsync_RacingOperationCompletion_NeverThrowsObjectDisposedException`

Verifies that a cancellation request the native `Generate` call does not honor within the abandon
timeout returns the canceled operation to the caller promptly (while the native call is still
running), reports the fault through diagnostics, moves the session to `Faulted`, and rejects
reuse; that `StopAsync` and `DisposeAsync` do not complete (and the engine lease is not released)
until the abandoned native call, or an abandoned playback-device start, has genuinely returned;
that a device stop is deferred until an abandoned start returns; that a caller token bounds only
that caller's own wait; and that concurrent and racing stop/dispose calls share one teardown
without throwing.

#### Session: Silence, Volume, and Unrequested Cancellation

**Tests**: `SynthesisSession_SpeakAsync_SegmentWithPreAndPostSilence_WritesSilenceAroundAudio`,
`SynthesisSession_SynthesizeAsync_VolumeOverride_ScalesAndClampsSamples`,
`SynthesisSession_SynthesizeAsync_SpeedOverride_PassesRatioToDefaultToBackend`,
`SynthesisSession_SynthesizeAsync_NonPositiveOverrideRatio_IsIgnored`,
`SynthesisSession_SynthesizeAsync_BackendThrowsUnrequestedOperationCanceledException_TransitionsToFaultedNotStopped`,
`SynthesisSession_SpeakAsync_VeryLongSilence_IsWrittenInBoundedChunks`,
`SynthesisSession_Operations_SynthesizedText_IsNeverReportedThroughDiagnostics`,
`SynthesisSession_DisposeAsync_IdleSessionHandlerBlocksOnOtherThreadReadingState_DoesNotDeadlock`

Verifies that a pause segment's silence is written to the device as zero samples sized for the
device's rate and channel count, with backend audio up-mixed to the device's channel count; that
a very long pause is written in bounded chunks; that
a volume tag scales the amplitude and a large boost is clamped to full scale; that an
`OperationCanceledException` thrown by the backend without a cancellation request is treated as
a fault, not a stop; that no diagnostic report contains the synthesized text; and that disposal
teardown never runs under the session lock.

#### Session: Default Backend Factory

**Tests**: `DefaultSynthesisBackendFactory_Create_ValidArguments_ReturnsModelCreatedBackend`,
`DefaultSynthesisBackendFactory_Create_InvalidArguments_Throws`

Verifies that the default factory returns the backend the model itself constructs and rejects a
null model and a null or empty directory.

#### Session: Voice/Speaker Selection

**Tests**: `SynthesisSession_SynthesizeAsync_NoParameterValues_ResolvesDefaultSpeakerIdFromModel`,
`SynthesisSession_SynthesizeAsync_ParameterValuesSupplied_ResolvesSpeakerIdFromBag`,
`SynthesisSession_SynthesizeAsync_ParameterValuesSuppliedAlongsideSpeedTag_BothMechanismsApplyIndependently`

Verifies that a session with no supplied `parameterValues` resolves to the model's own default
speaker id, that a supplied `parameterValues` bag resolves to the correct speaker id via
`ISynthesisModel.ResolveSpeakerId`, and that a session-level selected voice and an independent,
per-segment `[fast]` Natural Language Audio Tag speed override both apply correctly in the same
call, proving the two mechanisms coexist without either regressing the other.

#### Text Analysis: Validation and Span Mapping

**Tests**: `SegmentAnnotation_Constructor_NegativeStart_ThrowsArgumentOutOfRangeException`,
`SegmentAnnotation_Constructor_NonPositiveLength_ThrowsArgumentOutOfRangeException`,
`SegmentAnnotation_Constructor_BlankKind_Throws`,
`SegmentAnnotation_Constructor_NullValue_ThrowsArgumentNullException`,
`SegmentAnnotation_With_InvalidStart_ThrowsArgumentOutOfRangeException`,
`TextAnalysisResult_Constructor_NullAnnotations_ThrowsArgumentNullException`,
`TextAnalysisResult_Constructor_NullAnnotationElement_ThrowsArgumentException`,
`TextAnalysisResult_Constructor_NegativeBreak_ThrowsArgumentOutOfRangeException`,
`TextAnalysisResult_Constructor_CallerListsChangedLater_ResultIsUnaffected`,
`TextAnalysisResult_Constructor_UnsortedDuplicateBreaks_AreSortedAndDistinct`,
`TextAnalysisMapper_BuildAnalysisText_PlainAndTagSpans_UsesOnePlaceholderPerTag`,
`TextAnalysisMapper_BuildAnalysisText_PlainTextOnly_EqualsInput`,
`TextAnalysisMapper_Annotate_AnnotationInsideSecondSpan_RebasedToSpanLocalOffset`,
`TextAnalysisMapper_Annotate_TagSpans_PassThroughUnchanged`,
`TextAnalysisMapper_Annotate_AnnotationCoveringPlaceholder_Dropped`,
`TextAnalysisMapper_Annotate_AnnotationSpanningTwoPlainSpans_Dropped`,
`TextAnalysisMapper_Annotate_AnnotationBeyondTextLength_Dropped`,
`TextAnalysisMapper_Annotate_BreaksProvided_AssignedToContainingSpansWithLocalOffsets`,
`TextAnalysisMapper_Annotate_BreakBetweenConsecutiveTags_AttachedAfterPrecedingTagSpan`,
`TextAnalysisMapper_Annotate_BreakBeforeLeadingTag_Dropped`,
`TextAnalysisMapper_Annotate_BreakAfterTrailingTag_AttachedToLastTag`,
`TextAnalysisMapper_Annotate_BreaksNull_SpansKeepNullBreaks`,
`TextAnalysisMapper_Annotate_EmptyResult_ReturnsSameSpanListInstance`,
`TextAnalysisMapper_Annotate_NullArguments_Throw`,
`TaggedTextSpan_PlainText_Defaults_HaveNoAnnotationsAndNoBreaks`,
`TaggedTextSpan_Annotations_NullElement_ThrowsArgumentException`,
`TaggedTextSpan_SentenceBreaks_Negative_ThrowsArgumentOutOfRangeException`,
`TaggedTextSpan_PlainText_AnnotatedFactory_ExposesAnnotationsAndBreaks`,
`TaggedTextSpan_TagSpan_AcceptsSentenceBreaks`

Verifies that an annotation rejects invalid offsets, kinds, and values (including through `with`),
that an analysis result rejects missing or negative content and keeps its own sorted copy, that the
analysis text holds exactly one placeholder per recognized tag, that an annotation or sentence break
is assigned to the plain span containing it with span-local offsets, that an annotation covering a
placeholder, crossing two spans, or lying beyond the text is dropped rather than clipped, that a
break between two consecutive tags is attached to the preceding tag span (and one before a leading
tag is dropped), that spans keep `null` breaks when none were supplied and every
span gets a possibly empty list when they were, that an empty analysis returns the same span list
instance, and that plain-text and tag spans carry breaks (and plain-text spans annotations) that
default to none.

#### Layer 2 Rendering: Annotations, Analyzer Breaks, and Public Building Blocks

**Tests**: `DefaultModelCapabilityProfile_Render_AnnotatedSpan_SegmentCarriesRebasedAnnotation`,
`DefaultModelCapabilityProfile_Render_AnnotationAfterStrippedTag_RebasedIntoMergedBuffer`,
`DefaultModelCapabilityProfile_Render_NativeTag_AnnotationOffsetsShiftedPastInsertedTagText`,
`DefaultModelCapabilityProfile_Render_ParameterMappedTag_AnnotationsStayWithTheirOwnSegment`,
`DefaultModelCapabilityProfile_Render_PauseTag_AnnotationsStayWithTheirOwnSegment`,
`DefaultModelCapabilityProfile_Render_SpanBreaksSupplied_SplitsAtAnalyzerBreaksNotPunctuation`,
`DefaultModelCapabilityProfile_Render_SentenceBreakAtTagSeam_SplitsAtSeam`,
`DefaultModelCapabilityProfile_Render_TagSpanBreakBetweenConsecutiveTags_NoneSupport_SplitsAtSeam`,
`DefaultModelCapabilityProfile_Render_TagSpanBreakBetweenConsecutiveTags_NativeSupport_SplitsBetweenTagTexts`,
`DefaultModelCapabilityProfile_Render_TagSpanBreakBetweenConsecutiveTags_ParameterMappedSupport_TwoSegments`,
`DefaultModelCapabilityProfile_Render_TagSpanBreakBeforeOffsetZero_SplitsBeforeTag`,
`DefaultModelCapabilityProfile_Render_AuthoritativeEmptyBreaksAcrossTagRun_StaysOneSegment`,
`DefaultModelCapabilityProfile_Render_SpansWithoutAnnotations_SegmentsEqualPreChangeShape`,
`SpeechSegment_Annotations_DefaultsToEmpty`,
`SpeechSegment_Annotations_OutOfRange_ThrowsArgumentException`,
`SpeechSegment_Annotations_NullElement_ThrowsArgumentException`,
`SpeechSegment_Annotations_CallerListChangedLater_SegmentIsUnaffected`,
`SpeechSegment_With_ShorterTextWouldOrphanAnnotation_ThrowsArgumentException`,
`DefaultModelCapabilityProfile_Instance_IsPublicSingletonUsableByExternalProfile`,
`DefaultModelCapabilityProfile_RenderText_AnnotatedText_ReturnsSegmentsWithRebasedAnnotations`,
`DefaultModelCapabilityProfile_RenderText_EllipsisChunk_SetsEllipsisPostSilence`,
`DefaultModelCapabilityProfile_RenderText_NullText_ThrowsArgumentNullException`,
`DefaultModelCapabilityProfile_PauseConstants_HaveDocumentedValues`

Verifies that span annotations are delivered on the segment containing them with offsets relative
to that segment, across stripped, passed-through, pause, and parameter-mapped tags; that span
sentence breaks (including one at a tag seam and one between consecutive tags, carried by the tag
span under none, native and parameter-mapped support, with a control showing no break means no
split) split narration at the supplied position rather than
at punctuation; that spans without annotations render exactly as before; that a segment validates
and defensively copies its annotations and cannot be shortened beneath one; and that the default
profile singleton, `RenderText`, and the pause constants are public and usable from outside.

#### Chunking: Annotation Re-Basing, Analyzer Boundaries, and Golden Corpus

**Tests**: `SentenceChunker_Chunk_RepresentativeCorpus_MatchesGoldenChunks`,
`SentenceChunker_ChunkAligned_NoAnnotationsNoBreaks_EqualsChunkWithMetadata`,
`SentenceChunker_ChunkAligned_AnnotationWithinSecondSentence_OffsetRelativeToChunk`,
`SentenceChunker_ChunkAligned_LeadingWhitespace_OffsetAccountsForTrim`,
`SentenceChunker_ChunkAligned_WhitespaceBudgetSplit_AnnotationRebasedThroughCollapsedWhitespace`,
`SentenceChunker_ChunkAligned_DegenerateMerge_AnnotationBeforeMergedPunctuation_Preserved`,
`SentenceChunker_ChunkAligned_AnnotationSpanningChunkBoundary_Dropped`,
`SentenceChunker_ChunkAligned_AnnotationOnTrimmedWhitespace_Dropped`,
`SentenceChunker_ChunkAligned_LeadingDegenerateRunAnnotation_Dropped`,
`SentenceChunker_ChunkAligned_AnnotationOutOfRange_Dropped`,
`SentenceChunker_ChunkAligned_AnalyzerBreaks_OverridePunctuationHeuristic`,
`SentenceChunker_ChunkAligned_AnalyzerBreaks_SplitWithoutPunctuation`,
`SentenceChunker_ChunkAligned_EmptyBreaksList_NoPrimarySplitButClauseSplitStillApplies`,
`SentenceChunker_ChunkAligned_BreaksOutOfRangeIgnored`,
`SentenceChunker_ChunkAligned_AnalyzerBreaks_ClauseCommaStillSplits`,
`SentenceChunker_ChunkAligned_SurrogatePairLetterChunkWithBreaks_RetainedWithAnnotation`,
`SentenceChunker_ChunkAligned_SurrogatePairLetterChunkWithAnnotationsOnly_Retained`,
`SentenceChunker_Chunk_SurrogatePairLetterChunkNoAnalysis_KeepsLegacyFiltering`,
`SentenceChunker_ChunkAligned_SurrogatePairEmoji_StillDegenerate`,
`SentenceChunker_ChunkAligned_NullText_ThrowsArgumentNullException`,
`SentenceChunker_ChunkAligned_NonPositiveMaxLength_ThrowsArgumentOutOfRangeException`,
`SentenceChunk_Annotations_DefaultStruct_IsEmpty`

Verifies that the refactored chunker still produces, for a representative corpus of at least forty
inputs, exactly the chunks and ellipsis flags recorded from the pre-refactor implementation; that
each chunk receives only the annotations wholly inside it, re-based through trimming, whitespace
collapsing, and punctuation merges; that an annotation crossing a chunk boundary, on trimmed
whitespace, on a dropped degenerate run, or out of range is dropped; that supplied sentence breaks
replace the punctuation pass while the clause and budget passes still apply, an empty list means no
sentence splits, and out-of-range breaks are ignored; that a chunk whose only letters are
supplementary-plane characters is kept (with its annotation) when annotations or breaks are
supplied but filtered exactly as before when neither is, while an emoji-only chunk is still merged;
and that `ChunkAligned` rejects null text and
a non-positive length.

#### Session: Text Analysis Runs Once and Annotations Are Re-Based

**Tests**: `SynthesisSession_SynthesizeAsync_Analyzer_ReceivesWholeTextOnceWithTagPlaceholders`,
`SynthesisSession_SpeakAsync_Analyzer_IsInvokedOncePerRequest`,
`SynthesisSession_SynthesizeAsync_AnalyzerAnnotationAcrossStrippedTag_AnnotatedBackendReceivesRebasedOffsets`,
`SynthesisSession_SynthesizeAsync_AnalyzerAnnotationsAcrossChunks_EachChunkReceivesOwnOffsets`,
`SynthesisSession_SynthesizeAsync_AnalyzerAnnotationSpanningTag_IsDropped`,
`SynthesisSession_SynthesizeAsync_AnalyzerBreakAfterSurrogatePair_KeepsChunkAndAnnotation`

Verifies that the analyzer is called exactly once per request, for both `SynthesizeAsync` and
`SpeakAsync`, with the whole normalized text in which `Hello [short pause] world.` arrives as
`Hello <placeholder> world.`; that an annotation on `read` in `I've [whispers] read it.` lands on `read`
in the merged text of a model that strips the tag; that annotating every `read` in text split by
a pause tag and chunking gives each annotated backend call offsets relative to its own chunk; that
an annotation spanning a tag is dropped; and that a chunk consisting of one supplementary-plane
letter keeps its text and annotation when the analyzer breaks after it.

#### Session: Analyzer Sentence Breaks Drive Chunking

**Tests**: `SynthesisSession_SynthesizeAsync_AnalyzerSentenceBreaks_DriveChunking`,
`SynthesisSession_SynthesizeAsync_AnalyzerBreakBetweenConsecutiveTags_SplitsIntoTwoSegments`

Verifies that `Dr. Smith arrived. He left.` with an analyzer break at offset 18 produces exactly
two backend calls, `Dr. Smith arrived.` and `He left.`, where the same text without an analyzer
is split at the abbreviation's period, and that `First[fast][excited]Second` with an analyzer
break between the two tags produces the two backend calls `First` and `Second` (the analyzer
having seen `First<placeholder><placeholder>Second`) rather than one `FirstSecond` call.

#### Session: Analysis Does Not Block the Caller

**Tests**: `SynthesisSession_SynthesizeAsync_SlowAnalyzer_DoesNotBlockCallingThread`,
`SynthesisSession_SpeakAsync_SlowAnalyzer_DoesNotBlockCallingThread`

Verifies, for both `SynthesizeAsync` and `SpeakAsync`, that with an analyzer gated on a release
event the call returns an incomplete task while the analyzer is still blocked, that the analyzer ran
on a different managed thread from the caller, and that once released the request completes and the
backend receives the text.

#### Session: Annotated Versus Legacy Backend Delivery

**Tests**: `SynthesisSession_SynthesizeAsync_AnnotatedBackend_ReceivesAnnotationsWithSegmentText`,
`SynthesisSession_SynthesizeAsync_AnnotatedBackendSegmentWithoutAnnotations_UsesPlainGenerate`,
`SynthesisSession_SynthesizeAsync_ProfileSuppliedAnnotations_ReachAnnotatedBackendWithoutAnalyzer`,
`SynthesisSession_SynthesizeAsync_LegacyBackendWithAnalyzer_ReceivesPlainTextIdenticalToUnannotatedRun`

Verifies that an annotation-accepting backend receives `GenerateAnnotated` with the segment's text
and annotations only for segments that have annotations, and plain `Generate` for those that do not;
that annotations a capability profile attaches to its own segments reach the backend with no
analyzer involved; and that a legacy backend receives text identical to the annotated backend's
segment texts and to a run with no analyzer.

#### Session: No Analyzer Leaves Behavior Unchanged

**Tests**: `SynthesisSession_SynthesizeAsync_NoAnalyzer_BackendReceivesSameTextAsBefore`,
`SynthesisSession_SynthesizeAsync_AnalyzerReturnsNull_BehavesAsNoAnalyzer`,
`SynthesisSession_SynthesizeAsync_AnalyzerReturnsEmptyResult_BehavesAsNoAnalyzer`

Verifies that no analyzer, an analyzer returning `null`, and an analyzer returning an empty result
all produce the backend texts a run before text analysis existed produced, and never call
`GenerateAnnotated`.

#### Session: Analyzer Fault Containment and Cancellation

**Tests**:
`SynthesisSession_SynthesizeAsync_AnalyzerThrows_ContinuesUnannotatedAndReportsWarningWithoutText`,
`SynthesisSession_SynthesizeAsync_AnalyzerCanceled_PropagatesOperationCanceled`,
`SynthesisSession_SynthesizeAsync_CancelDuringSlowAnalysis_ThrowsCanceledAndSkipsBackend`,
`SynthesisSession_SynthesizeAsync_AnalyzerAlreadyCanceledBeforeStart_AnalyzerNotInvoked`

Verifies that an analyzer throwing an exception whose message holds text lets the request complete
with plain text, with exactly one `Warning` diagnostic that names the exception type and contains
neither the message nor the synthesized text, that an analyzer cancellation propagates as
`OperationCanceledException` with the backend never called, that canceling the caller's token
while a slow analyzer is blocked reaches the analyzer through the operation token, ends the request
canceled with the session `Stopped`, and never calls the backend, and that an operation whose token
is already canceled never invokes the analyzer.

#### DedicatedWorker: Cooperative Cancellation and Non-Cooperative Abandonment

**Tests**: `DedicatedWorker_Run_CooperativeCancellation_CompletesPromptly`,
`DedicatedWorker_Run_NonCooperativeDelegate_AbandonsAfterTimeoutAndReportsDiagnostics`,
`DedicatedWorker_Run_UsesLongRunningTaskCreationOption`

Verifies that a delegate honoring cancellation promptly lets `Run` complete promptly rather than
waiting out its full abandon timeout; that a delegate which never observes cancellation is
abandoned once the (injectable, test-shortened) abandon timeout elapses, with a `Warning`
diagnostic reported and the awaited call still returning control to its caller as canceled; and
that the delegate always runs on a `TaskCreationOptions.LongRunning` task.

#### Playback Format Conversion: Resampling, Anti-Aliasing, and Upmix

**Tests**: `PlaybackAudioResampler_Resample_EqualRates_CopiesUnchanged`,
`PlaybackAudioResampler_Resample_Upsample_ProducesInterpolatedSamples`,
`PlaybackAudioResampler_Resample_Downsample_ReducesSampleCount`,
`PlaybackAudioResampler_Resample_AboveTargetNyquistTone_IsAttenuated`,
`PlaybackAudioResampler_Resample_ShortInputDuringDownsampling_DoesNotThrow`,
`PlaybackAudioResampler_Resample_EmptyInput_ReturnsEmpty`,
`PlaybackAudioResampler_Resample_NonPositiveRate_ThrowsArgumentOutOfRangeException`,
`PlaybackAudioResampler_UpmixToChannels_SingleChannel_CopiesUnchanged`,
`PlaybackAudioResampler_UpmixToChannels_MultipleChannels_ReplicatesEachFrame`,
`PlaybackAudioResampler_UpmixToChannels_EmptyInput_ReturnsEmpty`,
`PlaybackAudioResampler_UpmixToChannels_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException`,
`PlaybackAudioResampler_Convert_DifferentRateAndChannels_ResamplesThenUpmixes`,
`PlaybackAudioResampler_Convert_SingleChannelTarget_ReturnsResampledWithoutUpmix`,
`PlaybackAudioResampler_Constructor_NonPositiveArgument_ThrowsArgumentOutOfRangeException`

Verifies identity pass-through at equal rates, interpolation-only upsampling, anti-aliased
downsampling, short-input safety, empty-input handling, single/multi-channel upmix, rejection of
non-positive arguments, the composed resample-then-upmix conversion, and the single-channel-target
fast path that skips the upmix step entirely.

#### Unavailable Fallback: Honest Degradation

**Tests**: `UnavailableSpeechSynthesizerEngine_IsAvailable_Read_ReturnsFalse`,
`UnavailableSpeechSynthesizerEngine_CreateSessionAsync_Always_ReturnsUnavailableSession`,
`UnavailableSpeechSynthesizerEngine_CreateSessionAsync_NullDevice_ThrowsArgumentNullException`,
`UnavailableSpeechSynthesizerEngine_SpeakAsync_Always_ThrowsSpeechSynthesizerUnavailableException`,
`UnavailableSpeechSynthesizerEngine_SynthesizeAsync_Always_ThrowsSpeechSynthesizerUnavailableException`,
`UnavailableSpeechSynthesizerEngine_DisposeAsync_CalledTwice_DoesNotThrow`,
`UnavailableSynthesisSession_IsAvailable_Read_ReturnsFalse`,
`UnavailableSynthesisSession_State_Read_ReturnsCreated`,
`UnavailableSynthesisSession_StateChanged_SubscribeAndUnsubscribe_DoesNotThrow`,
`UnavailableSynthesisSession_SpeakAsync_Always_ThrowsSpeechSynthesizerUnavailableException`,
`UnavailableSynthesisSession_SpeakAsync_NullText_ThrowsArgumentNullException`,
`UnavailableSynthesisSession_SynthesizeAsync_Always_ThrowsSpeechSynthesizerUnavailableException`,
`UnavailableSynthesisSession_SynthesizeAsync_NullText_ThrowsArgumentNullException`,
`UnavailableSynthesisSession_StopAsync_NoSessionInFlight_IsNoOp`,
`UnavailableSynthesisSession_DisposeAsync_CalledTwice_DoesNotThrow`,
`SpeechSynthesizerUnavailableException_Constructor_WithMessage_ExposesMessage`,
`SpeechSynthesizerUnavailableException_Constructor_WithInnerException_ExposesBoth`,
`SpeechSynthesizerUnavailableException_Constructor_Default_HasNonEmptyMessage`

Verifies that both the shared unavailable engine and the shared unavailable session stay honest
and safe to hold and dispose, that a device bound to an unavailable engine still succeeds (since
binding is an ordinary composition, not an error), that operational misuse throws the documented
exception, and that the exception conforms to the standard three-constructor pattern.
