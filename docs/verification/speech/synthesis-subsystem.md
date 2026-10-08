## SynthesisSubsystem Verification

### Verification Approach

The SynthesisSubsystem's Layer 1 scope - the closed Natural Language Audio Tag vocabulary and the
Layer 1 parser - is verified entirely through deterministic, pure unit tests over plain strings.
Neither `AudioTagCatalog` nor `AudioTagParser` depends on a model, an inference engine, an audio
device, or any native runtime, so no test double is required at this stage: every test calls the
production catalog/parser directly and asserts on their return values. This remains unchanged by
the Engine/Session redesign.

The Layer 3/Layer 5 Engine/Session API is verified the same way the RecognitionSubsystem's own
Engine/Session redesign is: a fake `ISynthesisBackend`/`ISynthesisBackendFactory` pair and an
NSubstitute `IAudioPlaybackDevice` stand in for the native sherpa-onnx runtime and real speakers,
making Layer 2 rendering, chunking, per-operation synthesize-then-play behavior, engine
exclusivity leasing, the session state machine, the overlap rule, cancellation, fault
containment, and honest degradation fully testable without a downloaded speech model or physical
audio hardware. Determinism is structural, not timing-based: a deterministic fake backend plus
`SemaphoreSlim`-based test doubles let a test assert ordering, in-flight state, and fault
transitions without polling or sleeping.

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

Automated coverage **does not** include synthesizing real, intelligible speech. Proving that real
text produces correct audible speech through a real model on real speakers requires a downloaded
production model (which this library deliberately does not ship by default) and audio hardware,
so it remains a manual/local verification activity, mirroring the RecognitionSubsystem's
identical boundary. The real, native-backed `SherpaOnnxSynthesisEngine` backend ships in the
sibling SpeechSherpa library; see _SpeechSherpa SynthesisSubsystem Verification_.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: None - no external services, no downloaded model, no native runtime, no audio
  hardware
- **Test doubles**: None for the vocabulary/parser tests. For the Engine/Session pipeline: a fake
  `ISynthesisBackend`/`ISynthesisBackendFactory` pair, NSubstitute playback devices (including a
  semaphore-signaling `PendingSampleCount` stub for the genuine-drain-wait test) and diagnostics
  sinks, and a fake synthesis model
- **Isolation**: Every vocabulary/parser test is a pure function call with no shared or persisted
  state; composition tests create and delete their own scratch installed-model directory

### Acceptance Criteria

A SynthesisSubsystem test run passes when:

- Every canonical `NaturalLanguageAudioTag` value has at least one alias in `AudioTagCatalog`,
  and no alias is shared by two different tags
- Every documented alias resolves back to its own canonical tag and kind
- Alias resolution is case-insensitive and tolerant of extra internal whitespace
- `AudioTagParser.Parse` returns literal text and recognized tags as an ordered sequence of
  spans that reconstructs the input's structure, for plain text alone, a tag alone, adjacent
  tags, and a tag at the start or end of the input, with no spurious empty spans
- An empty input returns an empty span list
- Unrecognized bracket content, empty brackets, unclosed brackets, and stray closing brackets are
  passed through as literal text, exactly as they appeared, rather than throwing or being
  discarded
- Pause tags always render as timed silence regardless of a model's declared audio-tag support;
  native support passes canonical bracket text through unchanged; parameter-mapped support maps
  the handful of tags with a numeric convention and strips every other tag; no support strips
  every non-pause tag; plain text is split into chunk-sized segments
- `SentenceChunker.Chunk` splits on primary sentence-ending punctuation first, then
  unconditionally splits every resulting sentence-level piece further on secondary clause
  punctuation, falls back to a whitespace-budget split only when still over budget, merges
  degenerate word-less pieces onto the preceding chunk, never splits a single word, and rejects a
  non-positive budget or null text. `ChunkWithMetadata` additionally flags, per chunk, whether the
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
  the lease so a subsequent request succeeds again
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

### Test Scenarios

#### Vocabulary: Completeness and Alias Resolution

**Tests**: `AudioTagCatalog_Tags_Always_CoversEveryCanonicalTagWithAtLeastOneAlias`,
`AudioTagCatalog_Tags_Always_NoAliasIsSharedByTwoDifferentTags`,
`AudioTagCatalog_TryResolve_EveryDocumentedAlias_ResolvesToItsOwnTag`

Verifies that every declared `NaturalLanguageAudioTag` value is reachable through at least one
alias, that no alias is ambiguous between two tags, and that every documented alias resolves
back to its own canonical tag and kind, sourced directly from `AudioTagCatalog.Tags` so this
coverage can never drift out of sync with the catalog it exercises.

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
`AudioTagCatalog_TryResolve_EmptyOrWhitespace_ReturnsFalse`

Verifies that an unknown word and empty or whitespace-only bracket content never resolve to a
tag.

#### Parsing: Span Sequence and Ordering

**Tests**: `AudioTagParser_Parse_EveryDocumentedAlias_ResolvesToItsCanonicalTagSpan`,
`AudioTagParser_Parse_PlainTextWithNoTags_ReturnsSinglePlainTextSpan`,
`AudioTagParser_Parse_EmptyInput_ReturnsEmptySpanList`,
`AudioTagParser_Parse_MixedPlainTextAndTag_ReturnsOrderedSpans`,
`AudioTagParser_Parse_AdjacentTags_ReturnsTwoSeparateTagSpans`,
`AudioTagParser_Parse_TagAtStartOfText_ReturnsTagSpanFirst`,
`AudioTagParser_Parse_TagAtEndOfText_ReturnsTagSpanLast`

Verifies that every documented alias, bracketed alone, resolves to a single tag span; that plain
text with no tags, and an empty input, each produce the expected trivial span list; and that
mixed text, adjacent tags, and tags at either edge of the input all produce correctly ordered
spans with no spurious empty spans.

#### Parsing: Malformed and Unrecognized Bracket Passthrough

**Tests**: `AudioTagParser_Parse_UnknownBracketedWord_PassesThroughAsLiteralText`,
`AudioTagParser_Parse_UnclosedOpeningBracket_PassesThroughAsLiteralText`,
`AudioTagParser_Parse_EmptyBrackets_PassesThroughAsLiteralText`,
`AudioTagParser_Parse_StrayClosingBracket_PassesThroughAsLiteralText`,
`AudioTagParser_Parse_RecognizedTagFollowedByUnknownBracket_ReturnsTagThenLiteralSpans`

Verifies that an unrecognized bracketed word, an unclosed opening bracket, empty brackets, and a
stray closing bracket all pass through as literal text exactly as they appeared, and that a
recognized tag immediately followed by an unrecognized bracket yields distinct, correctly
ordered tag and literal spans rather than merging or losing either.

#### Layer 2 Rendering: Pauses, Native, Parameter-Mapped, and Stripped Tags

**Tests**: `DefaultModelCapabilityProfile_Render_ShortPause_AlwaysRendersAsSilenceSegment`,
`DefaultModelCapabilityProfile_Render_LongPause_RendersLongerSilenceThanShortPause`,
`DefaultModelCapabilityProfile_Render_NativeSupport_PassesThroughCanonicalBracketText`,
`DefaultModelCapabilityProfile_Render_ParameterMappedSupport_FastTag_MapsToSpeedParameter`,
`DefaultModelCapabilityProfile_Render_ParameterMappedSupport_EmotionTag_StripsWithNoOverride`,
`DefaultModelCapabilityProfile_Render_NoneSupport_StripsAllTags`,
`DefaultModelCapabilityProfile_Render_PlainTextOnly_RendersChunkedSegments`,
`DefaultModelCapabilityProfile_Render_NullArguments_ThrowsArgumentNullException`,
`DefaultModelCapabilityProfile_Render_ChunkEndingInEllipsis_SetsLongerPostSilenceThanOrdinarySentenceEnd`,
`DefaultModelCapabilityProfile_Render_ChunkEndingInSpacedEllipsis_AlsoSetsEllipsisPostSilence`

Verifies that pauses always render as silence regardless of declared support, that native
support passes tags through unchanged, that parameter-mapped support maps the tags with a
built-in numeric convention and strips every other tag, that no support strips every non-pause
tag, that plain text is split into chunked segments, and that null arguments are rejected. Also
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
`SentenceChunker_Chunk_ClausePunctuationEmbeddedInNumeral_StaysAttached`,
`SentenceChunker_Chunk_SpacedPunctuationRun_MergesIntoPrecedingChunk`,
`SentenceChunker_Chunk_LoneSecondaryBoundaryWithNoWordContent_MergesIntoPrecedingChunk`,
`SentenceChunker_Chunk_LeadingDegenerateRun_IsDropped`,
`SentenceChunker_ChunkWithMetadata_TrailingAdjacentEllipsis_EndsWithEllipsisIsTrue`,
`SentenceChunker_ChunkWithMetadata_TrailingSpacedEllipsis_EndsWithEllipsisIsTrue`,
`SentenceChunker_ChunkWithMetadata_FewerThanThreeDots_EndsWithEllipsisIsFalse`,
`SentenceChunker_Chunk_AndChunkWithMetadata_ProduceSameChunkText`

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
three dots, and that `Chunk` produces exactly the same chunk text as `ChunkWithMetadata`.

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
`SpeechSynthesizerFactory_LoadAsync_CancelledToken_ThrowsOperationCanceledException`,
`SpeechSynthesizerFactory_LoadAsync_WithStoreNullModel_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_LoadAsync_WithStoreNullStore_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_LoadAsync_WithCatalogNullModel_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_LoadAsync_WithCatalogNullCatalog_ThrowsArgumentNullException`

Verifies that a null model, store, or catalog throws, and that a cancelled `cancellationToken`
throws `OperationCanceledException`, distinguishing a programming error/explicit cancellation
request from an ordinary machine state. Unlike the former synchronous factory, no
`NullPlaybackDevice` variant exists for any overload: `LoadAsync` no longer takes a playback
device parameter at all.

#### Composition: Parameter Value Forwarding and Validation

**Tests**: `SpeechSynthesizerFactory_LoadAsync_ParameterValuesSupplied_ForwardedToEngine`,
`SpeechSynthesizerFactory_LoadAsync_UnrecognizedParameterId_ComposesAndReportsInfo`,
`SpeechSynthesizerFactory_LoadAsync_RecognizedNumericParameterOutOfRange_Throws`,
`SpeechSynthesizerFactory_LoadAsync_RecognizedNumericParameterWrongType_Throws`,
`SpeechSynthesizerFactory_LoadAsync_RecognizedIntegerParameterFractionalValue_Throws`,
`SpeechSynthesizerFactory_LoadAsync_RecognizedChoiceParameterInvalidOption_Throws`

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
`SpeechSynthesizerEngine_DisposeAsync_WithActiveLeasedSession_DisposesSessionFirst`

Verifies that `CreateSessionAsync` succeeds immediately when no lease is held; fails fast with
`SynthesisEngineBusyException` (never queueing or waiting) when a lease is already held by a
still-undisposed session; succeeds again once that prior session is disposed, proving the lease is
released exactly once from the session's own `DisposeAsync`; rejects a null device; always reports
itself available; disposes its owned backend exactly once across repeated `DisposeAsync` calls;
and, when disposed while a session is still actively leased, disposes that session first (which
also releases the lease) before disposing the backend.

#### Engine: One-Shot Convenience Overloads

**Tests**: `SpeechSynthesizerEngine_SpeakAsync_CalledTwice_CreatesAndDisposesASessionEachTime`,
`SpeechSynthesizerEngine_SynthesizeAsync_NoDeviceSupplied_ReturnsSegments`

Verifies that the engine's one-shot `SpeakAsync` convenience overload creates and disposes a
fresh session per call (so repeated calls never conflict with the exclusivity lease), and that
`SynthesizeAsync` returns synthesized segments without requiring a playback device at all.

#### Session: Lifecycle State Machine and StateChanged Event

**Tests**: `SynthesisSession_StateChanged_OneSuccessfulOperation_RaisesExpectedTransitionsInOrder`,
`SynthesisSession_StateChanged_HandlerThrows_IsIsolatedAndDoesNotPropagate`,
`SynthesisSession_IsAvailable_BeforeAndAfterDispose_ReflectsLifecycle`

Verifies that one successful `SpeakAsync`/`SynthesizeAsync` call raises the documented
`Created → Starting → Running → Stopping → Stopped` sequence via `StateChanged`, in order; that a
subscriber's handler exception is caught and routed to diagnostics rather than propagated or
destabilizing the session; and that `IsAvailable` correctly reflects the session's lifecycle
before and after disposal.

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
`SynthesisSession_SynthesizeAsync_AfterFault_ThrowsSynthesisSessionFaultedException`

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
`SynthesisSession_SynthesizeAsync_LongMultiSentenceInput_ProducesOrderedSegmentsSequentially`

Verifies that plain text yields a synthesized audio segment, that `SpeakAsync` starts the
playback device, writes segments in order, and stops the device, and that a long, multi-sentence
input still yields segments in the correct order with the backend never called concurrently.

#### Session: Fault Containment

**Tests**: `SynthesisSession_SpeakAsync_PlaybackDeviceWriteThrows_PropagatesAndStillStopsDevice`,
`SynthesisSession_SpeakAsync_PlaybackDeviceUnavailable_ThrowsRatherThanHanging`,
`SynthesisSession_SpeakAsync_PlaybackDeviceStartThrows_StillCallsStop`

Verifies that a playback write failure still stops the device before propagating, that an
unavailable playback device fails promptly rather than hanging, and that a failure to start the
device still results in a stop attempt during teardown.

#### Session: Genuine Playback Drain Before Stopping

**Test**: `SynthesisSession_SpeakAsync_PlaybackDeviceReportsPendingSamples_WaitsForDrainBeforeStopping`

Verifies that `SpeakAsync` genuinely waits for the playback device to report a drained queue
before stopping it, rather than stopping as soon as every segment has been written. Uses an
NSubstitute playback device whose `PendingSampleCount` getter signals a semaphore on every read,
so the test deterministically observes the wait has genuinely begun (rather than racing a sleep)
before asserting the awaited task has not completed and `Stop()` has not been called; only after
the test sets the reported pending count to zero does the task complete and the device get
stopped.

#### Session: Cancellation and Disposal Lifecycle

**Tests**: `SynthesisSession_StopAsync_WhileSpeaking_CancelsInFlightOperationOnlyAfterInFlightGenerateReturns`,
`SynthesisSession_StopAsync_NoOperationInFlight_IsNoOp`,
`SynthesisSession_DisposeAsync_CalledTwice_ReleasesLeaseOnce`,
`SynthesisSession_SynthesizeAsync_AfterDispose_ThrowsObjectDisposedException`

Verifies that `StopAsync` cancels an in-flight operation deterministically, only after the
in-flight `DedicatedWorker`-routed `Generate` call has genuinely returned (never orphaning it); is
a safe no-op when no operation is in flight; releases the engine's exclusivity lease exactly once
across repeated `DisposeAsync` calls; and that operating on a disposed session throws
`ObjectDisposedException`.

#### Session: Voice/Speaker Selection

**Tests**: `SynthesisSession_SynthesizeAsync_NoParameterValues_ResolvesDefaultSpeakerIdFromModel`,
`SynthesisSession_SynthesizeAsync_ParameterValuesSupplied_ResolvesSpeakerIdFromBag`,
`SynthesisSession_SynthesizeAsync_ParameterValuesSuppliedAlongsideSpeedTag_BothMechanismsApplyIndependently`

Verifies that a session with no supplied `parameterValues` resolves to the model's own default
speaker id, that a supplied `parameterValues` bag resolves to the correct speaker id via
`ISynthesisModel.ResolveSpeakerId`, and that a session-level selected voice and an independent,
per-segment `[fast]` Natural Language Audio Tag speed override both apply correctly in the same
call, proving the two mechanisms coexist without either regressing the other.

#### DedicatedWorker: Cooperative Cancellation and Non-Cooperative Abandonment

**Tests**: `DedicatedWorker_Run_CooperativeCancellation_CompletesPromptly`,
`DedicatedWorker_Run_NonCooperativeDelegate_AbandonsAfterTimeoutAndReportsDiagnostics`,
`DedicatedWorker_Run_UsesLongRunningTaskCreationOption`

Verifies that a delegate honoring cancellation promptly lets `Run` complete promptly rather than
waiting out its full abandon timeout; that a delegate which never observes cancellation is
abandoned once the (injectable, test-shortened) abandon timeout elapses, with a `Warning`
diagnostic reported and the awaited call still returning control to its caller as cancelled; and
that the delegate always runs on a `TaskCreationOptions.LongRunning` task.

#### Playback Format Conversion: Resampling, Anti-Aliasing, and Upmix

**Tests**: `PlaybackAudioResampler_Resample_EqualRates_CopiesUnchanged`,
`PlaybackAudioResampler_Resample_Upsample_ProducesInterpolatedSamples`,
`PlaybackAudioResampler_Resample_Downsample_ReducesSampleCount`,
`PlaybackAudioResampler_Resample_AboveTargetNyquistTone_IsAttenuated`,
`PlaybackAudioResampler_Resample_ShortInputDuringDownsampling_DoesNotThrow`,
`PlaybackAudioResampler_Resample_EmptyInput_ReturnsEmpty`,
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
