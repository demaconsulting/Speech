## SynthesisSubsystem Verification

### Verification Approach

The SynthesisSubsystem's Sub-phase 4a scope - the closed Natural Language Audio Tag vocabulary
and the Layer 1 parser - is verified entirely through deterministic, pure unit tests over plain
strings. Neither `AudioTagCatalog` nor `AudioTagParser` depends on a model, an inference engine,
an audio device, or any native runtime, so no test double is required at this stage: every test
calls the production catalog/parser directly and asserts on their return values.

Sub-phase 4b, described below, is verified the same way the RecognitionSubsystem is: a fake
`ISynthesisEngine`/`ISynthesisEngineFactory` pair and an NSubstitute `IAudioPlaybackDevice` stand
in for the native sherpa-onnx runtime and real speakers, making Layer 2 rendering, chunking,
pipelined synthesize-while-play behavior, cancellation, fault containment, and honest degradation
fully testable without a downloaded speech model or physical audio hardware. Determinism is
structural, not timing-based: the bounded channel between the producer and the caller's
enumeration guarantees ordering without polling or sleeping.

Automated coverage **does not** include synthesizing real, intelligible speech. Proving that real
text produces correct audible speech through a real model on real speakers requires a downloaded
production model (which this phase deliberately does not ship) and audio hardware, so it remains
a manual/local verification activity, mirroring the RecognitionSubsystem's identical boundary.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: None - no external services, no downloaded model, no native runtime, no audio
  hardware
- **Test doubles**: None for the Sub-phase 4a vocabulary/parser tests. For Sub-phase 4b: a fake
  `ISynthesisEngine`/`ISynthesisEngineFactory` pair, NSubstitute playback devices and diagnostics
  sinks, and a fake synthesis model
- **Isolation**: Every Sub-phase 4a test is a pure function call with no shared or persisted
  state; Sub-phase 4b composition tests create and delete their own scratch installed-model
  directory

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
- `SentenceChunker.Chunk` splits on the coarsest boundary that still fits the configured budget,
  never splits a single word, and rejects a non-positive budget or null text
- Composition returns a real synthesizer only when the model is installed, declares the
  synthesis role, the playback device is available, and the engine loads; every other outcome
  returns the honest unavailable synthesizer without throwing
- A supplied `parameterValues` key naming a parameter not declared by the requested model is
  silently ignored (with only an `Info` diagnostic reported) and composition still succeeds; a
  supplied value for a parameter the model *does* declare that fails that parameter's own
  validation (wrong CLR type, out-of-range or non-integral for a `NumericParameter`, an invalid
  option for a `ChoiceParameter`, a non-`bool` for a `BooleanParameter`) throws
  `ArgumentException` synchronously from `Create()`, before any installed/role/device/engine
  check runs
- Text flows through chunking, rendering, and the engine to yield ordered audio segments, played
  in order with correct pre/post silence, while a later chunk synthesizes during an earlier
  chunk's playback
- A long, multi-sentence input still yields segments in the correct order at the increased
  look-ahead capacity of 5 pending segments, and the single background producer never calls
  `ISynthesisEngine.Generate` concurrently
- `Stop()` cancels an in-flight session deterministically and is a safe no-op when idle; engine
  faults and an unavailable playback device fail the caller's task honestly rather than hanging
- `PlaybackAudioResampler` resamples and upmixes engine-rate mono audio to the playback device's
  resolved format
- The unavailable synthesizer stays honest and safe to hold and dispose
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
`DefaultModelCapabilityProfile_Render_NullArguments_ThrowsArgumentNullException`

Verifies that pauses always render as silence regardless of declared support, that native
support passes tags through unchanged, that parameter-mapped support maps the tags with a
built-in numeric convention and strips every other tag, that no support strips every non-pause
tag, that plain text is split into chunked segments, and that null arguments are rejected.

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
`SentenceChunker_Chunk_NullText_ThrowsArgumentNullException`

Verifies that chunking prefers the coarsest boundary that still fits the budget, falls back
through clause punctuation to a whitespace budget only as needed, never splits a single
over-length word, and rejects a non-positive budget or null text. Also verifies that a maximal
run of consecutive primary sentence-ending characters (an ellipsis `...`, or mixed terminators
such as `?!`/`!!`) is treated as a single boundary and stays attached to the preceding sentence
as one piece - rather than producing degenerate single-punctuation-character chunks that cause
audible synthesis glitches - both mid-text and at the very end of the text, while a normal
single-terminator sentence is unaffected and the secondary/whitespace-budget passes remain
unchanged.

#### Composition: Real Synthesizer for an Installed Model and Available Device

**Tests**: `SpeechSynthesizerFactory_Create_ModelInstalledAndDeviceAvailable_ReturnsRealSynthesizer`,
`SpeechSynthesizerFactory_Create_WithStoreModelInstalledAndDeviceAvailable_ReturnsRealSynthesizer`,
`SpeechSynthesizerFactory_Create_WithCatalogModelInstalledAndDeviceAvailable_ReturnsRealSynthesizer`

Verifies that an installed synthesis model plus an available playback device composes a real
synthesizer wired to the injected engine factory, with the installed-model directory passed
through unchanged, whether that directory is supplied directly as a `string`, resolved from a
`SpeechModelStore`, or resolved from a `SpeechModelCatalog`'s own store.

#### Composition: Honest Fallback for Every Unavailable State

**Tests**: `SpeechSynthesizerFactory_Create_ModelNotInstalled_ReturnsUnavailableSynthesizer`,
`SpeechSynthesizerFactory_Create_PlaybackDeviceUnavailable_ReturnsUnavailableSynthesizer`,
`SpeechSynthesizerFactory_Create_ModelRoleIsNotSynthesis_ReturnsUnavailableSynthesizer`,
`SpeechSynthesizerFactory_Create_EngineLoadFails_ReturnsUnavailableSynthesizerAndDoesNotThrow`,
`SpeechSynthesizerFactory_Create_WithStoreModelNotInstalled_ReturnsUnavailableSynthesizer`,
`SpeechSynthesizerFactory_Create_WithCatalogModelNotInstalled_ReturnsUnavailableSynthesizer`

Verifies that a missing model, an unavailable device, a wrong-role model, and a failed engine
load all degrade to the shared unavailable synthesizer without throwing.

#### Composition: Null Arguments Are Programming Errors

**Tests**: `SpeechSynthesizerFactory_Create_NullModel_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_Create_NullPlaybackDevice_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_Create_WithStoreNullModel_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_Create_WithStoreNullStore_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_Create_WithStoreNullPlaybackDevice_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_Create_WithCatalogNullModel_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_Create_WithCatalogNullCatalog_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_Create_WithCatalogNullPlaybackDevice_ThrowsArgumentNullException`

Verifies that a null model, playback device, store, or catalog throws, distinguishing a
programming error from an ordinary machine state.

#### Composition: Voice Selection Value Bag Forwarding

**Test**: `SpeechSynthesizerFactory_Create_ParameterValuesSupplied_ForwardedToSynthesizer`

Verifies that an optional `parameterValues` bag supplied by the caller (for example, a chosen
voice built from a declared `ChoiceParameter`) is forwarded unchanged to the constructed
synthesizer.

#### Composition: Parameter Value Validation

**Tests**: `SpeechSynthesizerFactory_Create_UnrecognizedParameterId_ComposesAndReportsInfo`,
`SpeechSynthesizerFactory_Create_RecognizedNumericParameterOutOfRange_Throws`,
`SpeechSynthesizerFactory_Create_RecognizedNumericParameterWrongType_Throws`,
`SpeechSynthesizerFactory_Create_RecognizedIntegerParameterFractionalValue_Throws`,
`SpeechSynthesizerFactory_Create_RecognizedChoiceParameterInvalidOption_Throws`

Verifies the deliberate, breaking-change split introduced for this behavior: a supplied
`parameterValues` key naming a parameter the model does not declare still composes a real
synthesizer and reports only an `Info` diagnostic, never throwing (preserving cross-model
compatibility); a supplied value for a parameter the model *does* declare, but that is invalid
for it, throws `ArgumentException` synchronously from `Create()` - before any
installed/role/device/engine check runs - naming the parameter id, the model id, and the specific
reason the value is invalid. This replaces this library's earlier behavior of silently
substituting a default the first time `ResolveSpeakerId` ran per segment, and does not change
`ResolveSpeakerId`'s or `ResolveOverrideRatios`'s own existing never-throw, per-segment runtime
contract.

#### Pipeline: Chunked Synthesis and Ordered Playback

**Tests**: `SynthesizeStreamAsync_PlainText_YieldsAudioSegment`,
`SynthesizeStreamAsync_TextWithPauseTag_YieldsSilenceSegmentWithNoEngineCall`,
`PlayStreamAsync_OrderedSegments_StartsWritesInOrderAndStops`

Verifies that plain text yields a synthesized audio segment, that a pause tag yields a
pure-silence segment without invoking the engine, and that ordered segments are started, written
in order, and stopped on the playback device.

#### Pipeline: Ordered, Strictly Sequential Production at the Increased Look-Ahead Capacity

**Test**: `SynthesizeStreamAsync_LongMultiSentenceInput_ProducesOrderedSegmentsSequentially`

Verifies the `PendingSegmentCapacity` tuning change from `2` to `5`: for a long, 12-sentence
input - producing more chunks than fit in the buffer at once - the yielded segments still arrive
in the exact same order the sentences appear in the source text (cross-checked against each
chunk's expected sample count from `FakeSynthesisEngine`), and `FakeSynthesisEngine`'s
concurrency tracking (`MaxConcurrentGenerateCalls`) reports a maximum of exactly `1`, proving the
single background producer never calls `ISynthesisEngine.Generate` concurrently even with up to
5 chunks now pipelined ahead of playback.

#### Pipeline: Fault Containment

**Tests**: `SynthesizeStreamAsync_EngineThrows_ReportsFaultAndPropagatesToCaller`,
`PlayStreamAsync_PlaybackDeviceWriteThrows_PropagatesAndStillStopsDevice`,
`PlayStreamAsync_PlaybackDeviceUnavailable_ThrowsRatherThanHanging`

Verifies that an engine fault is reported and propagated to the caller rather than hanging, that
a playback write failure still stops the device before propagating, and that an unavailable
playback device fails promptly rather than hanging the pipeline.

#### Pipeline: Genuine Playback Drain Before Stopping

**Tests**: `PlayStreamAsync_PlaybackDeviceReportsPendingSamples_WaitsForDrainBeforeStopping`

Verifies the fix for a bug where the TTS panel's status flashed from "Playing" back to "Idle"
almost instantly with no audible sound: `PlayStreamAsync` now polls the playback device's
`PendingSampleCount` and does not stop the device merely because every segment has been
enqueued. Uses an NSubstitute playback device whose `PendingSampleCount` getter signals a
semaphore on every read, so the test deterministically observes the wait has genuinely begun
(rather than racing a sleep) before asserting the awaited task has not completed and `Stop()` has
not been called; only after the test sets the reported pending count to zero does the task
complete and the device get stopped.

#### Pipeline: Cancellation and Lifecycle

**Tests**: `Stop_WhileSpeaking_CancelsInFlightSessionOnlyAfterInFlightGenerateReturns`,
`SynthesizeStreamAsync_CancelledMidGenerate_AwaitsProducerBeforeEnumerationCompletesAndDisposalIsSafe`,
`Stop_NoSessionInFlight_IsNoOp`, `Dispose_CalledTwice_DisposesEngineOnce`,
`SynthesizeStreamAsync_AfterDispose_ThrowsObjectDisposedException`, `IsAvailable_Always_ReturnsTrue`

Verifies that `Stop()` cancels an in-flight session deterministically, is a safe no-op when idle,
disposal releases the engine exactly once even when called twice, operating after disposal is
rejected, and a real synthesizer always reports itself available. Also verifies the fix for a
confirmed `AccessViolationException` crash: `SynthesizeStreamAsync`/`SpeakAsync` never report
completion while the producer's in-flight native `Generate` call is still running, on either the
`Stop()`-driven or the directly-cancelled path, so a caller can never dispose the engine out from
under a still-executing call. A `BlockingSynthesisEngine` test double holds `Generate` open on two
`SemaphoreSlim`s until the test explicitly releases it, letting each test assert the outer task is
still incomplete immediately after cancellation and only completes (with `OperationCanceledException`)
once the in-flight call has genuinely returned; the second test additionally disposes the
synthesizer immediately afterward and asserts no exception, and that only the one expected
`Generate` call was ever made.

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
`PlaybackAudioResampler_Constructor_NonPositiveArgument_ThrowsArgumentOutOfRangeException`

Verifies identity pass-through at equal rates, interpolation-only upsampling, anti-aliased
downsampling, short-input safety, empty-input handling, single/multi-channel upmix, rejection of
non-positive arguments, and the composed resample-then-upmix conversion.

#### Unavailable Fallback: Honest Degradation

**Tests**: `UnavailableSpeechSynthesizer_IsAvailable_Read_ReturnsFalse`,
`UnavailableSpeechSynthesizer_SynthesizeStreamAsync_Always_ThrowsSpeechSynthesizerUnavailableException`,
`UnavailableSpeechSynthesizer_PlayStreamAsync_Always_ThrowsSpeechSynthesizerUnavailableException`,
`UnavailableSpeechSynthesizer_SpeakAsync_Always_ThrowsSpeechSynthesizerUnavailableException`,
`UnavailableSpeechSynthesizer_Stop_Always_ThrowsSpeechSynthesizerUnavailableException`,
`UnavailableSpeechSynthesizer_Dispose_CalledTwice_DoesNotThrow`,
`SpeechSynthesizerUnavailableException_Constructor_WithMessage_ExposesMessage`,
`SpeechSynthesizerUnavailableException_Constructor_WithInnerException_ExposesBoth`,
`SpeechSynthesizerUnavailableException_Constructor_Default_HasNonEmptyMessage`

Verifies that the shared fallback stays honest and safe to hold and dispose, that operational
misuse throws the documented exception, and that the exception conforms to the standard
three-constructor pattern.
