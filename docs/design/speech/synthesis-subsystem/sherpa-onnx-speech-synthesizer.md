### SherpaOnnxSpeechSynthesizer

This chapter covers the streaming synthesizer together with the units it exists to coordinate -
the Layer 2 rendering strategy, the sentence chunker, the synthesis-engine seam, and the
playback-format converter - because none can be reviewed meaningfully in isolation: the
synthesizer's whole job is to move text through rendering and chunking, into the engine, and the
resulting audio through resampling onto the playback device, all while pipelining synthesis with
playback.

**Purpose**: Chunk, render, and synthesize text into speech through a synthesis engine, and play
the result while continuing to synthesize later chunks, without ever performing synthesis or
inference work on the caller's UI thread.

**Data Model**: Holds the owned `ISynthesisEngine`, the `IAudioPlaybackDevice` it plays through,
the `ISynthesisModel` supplying `NormalizeText`/`CapabilityProfile`/`ResolveSpeakerId`, an
optional `parameterValues` bag (the session's selected voice/tunable-parameter values, forwarded
unchanged from `SpeechSynthesizerFactory.Create`), and a diagnostics sink. While a session is in
flight it also holds a `CancellationTokenSource` linked to the caller's token (guarded by a lock,
so `Stop()` can cancel it safely from another thread) and, per `SynthesizeStreamAsync` call, a
bounded `Channel<SynthesizedSpeech>` of capacity 5 together with the background producer `Task`
filling it. `IsAvailable` is always `true`, because this type is only ever created after the
engine loaded and the device reported itself available - every unavailable case is represented by
`UnavailableSpeechSynthesizer` instead.

The pending-segment channel holds at most 5 synthesized segments (raised from an original 2 to
smooth pacing over long multi-sentence text, since 2 could let playback catch up to and stall on
a still-synthesizing segment whenever one chunk took noticeably longer than its predecessor took
to play) and uses `BoundedChannelFullMode.Wait`: unlike the recognition-direction capture queue,
which can drop the oldest live audio, a segment here has already cost real inference time and
must never be silently discarded, so the producer simply waits for the consumer (playback) to
catch up. This is purely a buffer-size tuning change: it does not reduce the latency before the
very first word is spoken, which remains bounded by however long the first chunk alone takes to
synthesize.

`DrainPollInterval` (15ms) and `DrainTailMargin` (40ms) are fixed constants governing
`WaitForPlaybackDrainAsync`'s post-loop wait in `PlayStreamAsync` (see below): short enough that
`PlayStreamAsync` returns promptly once the hardware is genuinely done, long enough to avoid
busy-spinning the thread pool re-reading a value only a real-time audio callback thread can
change.

**Key Methods**:

- **SynthesizeStreamAsync(text, cancellationToken)**: Validates arguments eagerly (split into a
  non-iterator validating wrapper plus a separate iterator method, `SynthesizeStreamCore`, so
  argument errors surface on the calling `MoveNextAsync` rather than being deferred - a SonarQube
  S4456 requirement for async-iterator methods with parameter validation). Normalizes the text via
  `_model.NormalizeText`, parses it via `AudioTagParser.Parse`, renders it via
  `_model.CapabilityProfile.Render(spans, _model)` into a `SpeechPlan`, spawns a producer task
  synthesizing each `SpeechSegment` in turn onto the bounded channel, and yields from
  `channel.Reader.ReadAllAsync(...)` inside a `try`/`finally` whose `finally` block unconditionally
  awaits the producer task - on normal completion, on cancellation, and on any other exception
  from the loop alike - so the producer (and whatever in-flight `_engine.Generate` call it may be
  mid-way through) is guaranteed to have genuinely finished before this method ever returns
  control to its caller. This closes a use-after-free window that previously existed only on the
  cancellation path: `ReadAllAsync(cancellationToken)` observing cancellation threw
  `OperationCanceledException` straight out of the `await foreach`, skipping a then-unconditional
  post-loop await and orphaning the producer task; because the producer only checks its own
  cancellation token between segments (never while inside `GenerateSegment`), an in-flight native
  `_engine.Generate` call kept running, untracked, on a background thread even after this method
  returned - and if the caller (for example `SpeakAsync`'s caller, on observing the same
  cancellation) then disposed the synthesizer and its owned engine, that still-running native call
  touched freed native memory, producing an `AccessViolationException` on the ThreadPool worker
  thread running the producer. The `finally` block's await swallows a residual
  `OperationCanceledException` from the producer task specifically (benign and expected on the
  cancellation path, since `ProduceAsync` normally suppresses it internally and completes the
  channel instead of faulting) without masking whatever exception, if any, is already propagating
  out of the loop; a genuine (non-cancellation) producer fault on the normal-completion path still
  propagates to the caller exactly as before. See
  `SherpaOnnxSpeechSynthesizerTests.Stop_WhileSpeaking_CancelsInFlightSessionOnlyAfterInFlightGenerateReturns`
  and
  `SherpaOnnxSpeechSynthesizerTests.SynthesizeStreamAsync_CancelledMidGenerate_AwaitsProducerBeforeEnumerationCompletesAndDisposalIsSafe`
  for regression coverage proving the producer task is never orphaned and disposal after
  cancellation is safe.
- **GenerateSegment(segment)**: An empty-text segment (a rendered pause) skips the engine
  entirely and returns a pure-silence `SynthesizedSpeech` built directly from the segment's
  declared silence durations. Otherwise resolves speed/volume overrides against the model's
  declared numeric parameters via `SpeechParameterConventions`, resolves the speaker id for this
  session's selected voice by calling `_model.ResolveSpeakerId(_parameterValues)` once per
  segment (cheap and pure - re-evaluated fresh each call rather than cached for the whole
  session, so it always reflects the constructor-supplied bag; this is deliberately independent
  of the per-segment `ParameterOverrides` speed/volume mechanism above, which is Natural Language
  Audio Tag-scoped and transient, not a fit for a session-level voice selection), calls
  `_engine.Generate(text, speedRatio, speakerId)`, and applies any volume override as post-hoc
  amplitude scaling (sherpa-onnx's offline TTS API has no native gain input), clamped to
  `[-1.0, 1.0]`.
- **PlayStreamAsync(stream, cancellationToken)**: Calls `_playbackDevice.Start()`, builds one
  `PlaybackAudioResampler` for the call from the engine's and device's reported formats, and for
  each segment writes its pre-silence, resampled/upmixed audio, and post-silence in order. Once
  every segment has been written, calls `WaitForPlaybackDrainAsync` before returning: `Write` is
  fire-and-forget, so having enqueued every segment does not mean the hardware has rendered any
  of it yet, and treating enqueue-complete as playback-complete was the root cause of a bug where
  the TTS panel's status flashed back to idle and cut audio off almost instantly. A `finally`
  block calls `_playbackDevice.Stop()` regardless of whether the loop, the drain wait, or neither
  completed, faulted, or was cancelled - reporting (but not rethrowing) a failure to stop, so the
  device is never left running.
- **WaitForPlaybackDrainAsync(cancellationToken)**: Polls `_playbackDevice.PendingSampleCount`
  every `DrainPollInterval` (15ms) until it reaches zero - i.e. until the playback hardware has
  genuinely rendered every sample this session wrote, not merely until every segment was handed
  off - then applies one further `DrainTailMargin` (40ms) wait before returning, to absorb any
  residual internal host buffering (e.g. PortAudio's own ring buffer) that has already left the
  managed queue but has not yet actually reached the speakers. Honors `cancellationToken`: a
  cancellation during either wait ends it immediately via `Task.Delay`'s own cancellation, letting
  the caller's `finally` block still stop the device promptly instead of waiting out the full
  drain.
- **SpeakAsync(text, cancellationToken)**: Creates the session's linked `CancellationTokenSource`
  under the lock, composes `SynthesizeStreamAsync` with `PlayStreamAsync`, and clears the field
  and disposes the token source in a `finally` block regardless of outcome.
- **Stop()**: Cancels `_sessionCancellation` if a session is in flight; a safe no-op otherwise.
- **Dispose()**: Calls `Stop()` then disposes the owned engine. Idempotent.

**Error Handling**: A fault raised synthesizing a segment is caught by the producer task,
reported through `ISpeechDiagnostics`, and completes the channel with that exception
(`writer.TryComplete(ex)`) so the awaiting consumer observes it as a faulted enumeration rather
than hanging; a cancellation during production completes the channel normally instead.
`SynthesizeStreamCore`'s `finally` block guarantees the producer task is always awaited to
completion before the method returns on every exit path, so a caller can never observe control
returned while an in-flight `_engine.Generate` call is still running - see
`SynthesizeStreamAsync(text, cancellationToken)` above for the full rationale and regression
tests. A playback-device write failure propagates out of `PlayStreamAsync` after the device is still
stopped in `finally`. A cancellation while `WaitForPlaybackDrainAsync` is polling for drain (or
during its tail margin) propagates the same `Task.Delay`-raised `OperationCanceledException`,
ending the wait immediately rather than waiting out the full drain, while `finally` still stops
the device. An unavailable playback device throws promptly rather than hanging.
Calling an operational member after disposal throws `ObjectDisposedException`.

**Dependencies**: `ISynthesisEngine`, `SpeechPlan`, `SpeechSegment`, `SpeechParameterConventions`,
`PlaybackAudioResampler`, `SynthesizedSpeech`, `SpeechSynthesizerUnavailableException`,
`AudioTagParser` from this subsystem's Sub-phase 4a units, `IAudioPlaybackDevice` from the
AudioSubsystem, and `ISpeechDiagnostics` from the Diagnostics subsystem.

**Callers**: `SpeechSynthesizerFactory.Create(model, installedModelDirectory, playbackDevice,
diagnostics, parameterValues)`.

#### ISynthesisEngine and ISynthesisEngineFactory

**Purpose**: Confine every speech-synthesis interop call behind one mockable boundary, so the
synthesizer's chunking, Layer 2 rendering, pipelining, and fault-containment logic is verifiable
with pure managed fakes - no downloaded model and no platform-specific native binary are ever
required in CI. This mirrors the `IRecognitionEngine`/`IRecognitionEngineFactory` seam used for
recognition.

**Data Model**: N/A (interfaces only).

**Key Methods**:

- **ISynthesisEngine.SampleRate**: The fixed rate this engine's `Generate(...)` output is
  produced at.
- **ISynthesisEngine.Generate(text, speed, speakerId)**: Synthesizes one chunk of text at the
  given speed multiplier and speaker id, returning an `EngineAudio` (samples plus the rate they
  were produced at).
- **ISynthesisEngineFactory.Create(ISynthesisModel, string installedModelDirectory)**: Loads an
  engine from the model's own declared configuration.

**Error Handling**: Implementations are not thread-safe by contract; the synthesizer calls
`Generate` from exactly one producer thread per session. Load failures surface as exceptions from
`Create(...)`, which `SpeechSynthesizerFactory` converts into the honest unavailable fallback.

**Dependencies**: `EngineAudio`; `ISynthesisModel` from the ModelManagementSubsystem.

**Callers**: `SherpaOnnxSpeechSynthesizer` (engine) and `SpeechSynthesizerFactory` (factory).

#### SherpaOnnxSynthesisEngine and SherpaOnnxSynthesisEngineFactory

**Purpose**: Implement the engine seam against the real sherpa-onnx offline TTS API, reusing the
already-referenced `org.k2fsa.sherpa.onnx` package. These are the only types in this
subsystem that call speech-synthesis inference APIs.

**Data Model**: The engine holds one loaded `OfflineTts`, its declared `SampleRate`, and a
disposed flag. The factory is stateless and holds no model-specific knowledge at all -
the design makes each model's backing class responsible for its own engine configuration,
so adding a synthesis model never requires changing the factory.

**Key Methods**:

- **Generate(...)**: Calls the underlying `OfflineTts.Generate(text, speed, speakerId)` and wraps
  its output samples and rate in an `EngineAudio`.
- **Create(...)**: Asks the model for its `OfflineTtsConfig`, resolved against the
  installed-files directory, and loads an engine from it.
- **Dispose()**: Releases the loaded `OfflineTts`. Safe to call more than once.

**Error Handling**: Construction loads the model into native memory and therefore throws when the
native runtime for the current platform is absent or the model files are unusable; that exception
is what `SpeechSynthesizerFactory` converts into the honest unavailable fallback. Operational
members throw `ObjectDisposedException` after disposal.

**Dependencies**: The sherpa-onnx managed API (see _SherpaOnnx Design_); `ISynthesisModel` from
the ModelManagementSubsystem.

**Callers**: `SpeechSynthesizerFactory` constructs the factory; the factory constructs the
engine; `SherpaOnnxSpeechSynthesizer` uses the engine.

#### IModelCapabilityProfile and DefaultModelCapabilityProfile

**Purpose**: Decide, per Layer 1 span, how a Natural Language Audio Tag is realized for a
specific model - pass-through, approximation, or strip - turning a model-independent span
sequence into an ordered, model-appropriate `SpeechPlan`.

**Data Model**: `IModelCapabilityProfile` declares one method,
`Render(IReadOnlyList<TaggedTextSpan> spans, ISpeechModel model)`, returning a `SpeechPlan` (an
ordered `IReadOnlyList<SpeechSegment>`). Each `SpeechSegment` carries the text to synthesize (or
empty, for a pure-silence pause segment), the pre/post silence durations to play alongside it,
and any numeric parameter overrides (speed/volume) to apply when synthesizing it.
`DefaultModelCapabilityProfile` is a stateless singleton (`Instance`) requiring no per-model
configuration, since every decision it makes is read from the `model` argument at call time.

This signature intentionally differs from the parameter-dictionary shape sketched in the Phase 4
plan (see the subsystem Design section above for the full rationale): a stateless singleton
shared by every model has no per-call parameter bag to consume, and reading everything from
`model` directly keeps the contract satisfiable with zero code by any model that wants the
default, generically-correct behavior.

**Key Methods**:

- **Render(spans, model)**: For each `TaggedTextSpanKind.Tag` span: if the tag is a pause, always
  renders a silence-only `SpeechSegment` (300ms short / 900ms long) regardless of `model
  .AudioTagSupport`. Otherwise, if `model.AudioTagSupport` is `Native`, passes the tag through as
  its canonical bracket text so the model's own inference sees the control token. If
  `ParameterMapped`, consults `SpeechParameterConventions` for a matching numeric parameter on
  `model.Parameters` and attaches it as an override on the surrounding segment when one exists,
  otherwise strips the tag. If `None`, strips every non-pause tag. For each
  `TaggedTextSpanKind.PlainText` span, delegates to `SentenceChunker.Chunk(...)` and emits one
  `SpeechSegment` per resulting chunk.

**Error Handling**: Rejects a null `spans` or `model` with `ArgumentNullException`. Every other
input - any tag, any support level, any parameter set - is handled without throwing, per
this library's "never worse than plain narration" guarantee extended to Layer 2.

**Dependencies**: `TaggedTextSpan`, `TaggedTextSpanKind`, `NaturalLanguageAudioTagKind` from this
subsystem's Sub-phase 4a units; `SentenceChunker`; `SpeechParameterConventions`; `SpeechSegment`,
`SpeechPlan`; `ISpeechModel`, `SpeechModelAudioTagSupport`, `NumericParameter` from the
ModelManagementSubsystem.

**Callers**: `SherpaOnnxSpeechSynthesizer.SynthesizeStreamAsync(...)` via
`ISynthesisModel.CapabilityProfile`.

#### SentenceChunker

**Purpose**: Split synthesis-ready plain text into sentence/clause-sized chunks so chunked,
pipelined synthesis has natural-sounding boundaries and no chunk exceeds a length a synthesis
call can reasonably handle.

**Data Model**: A stateless static class; `Chunk(text, maxLength)` takes no configuration beyond
its two arguments.

**Key Methods**:

- **Chunk(text, maxLength)**: Splits on primary sentence-ending punctuation (`.`, `!`, `?`)
  first. A resulting piece still longer than `maxLength` is split further on secondary clause
  punctuation (`,`, `;`, `:`). A piece still longer than `maxLength` with no punctuation at all is
  split on a whitespace budget. A single word that alone exceeds `maxLength` is returned whole,
  never split mid-word.

**Error Handling**: Rejects a non-positive `maxLength` with `ArgumentOutOfRangeException` and a
null `text` with `ArgumentNullException`, since neither describes a meaningful chunking request.
Empty and whitespace-only text return no chunks rather than throwing.

**Dependencies**: None beyond the Base Class Library.

**Callers**: `DefaultModelCapabilityProfile.Render(...)`.

#### PlaybackAudioResampler

**Purpose**: Convert one synthesized segment's mono audio, produced at a synthesis engine's fixed
rate, into the format a playback device requires - its resolved rate and channel count. Keeping
this conversion in a pure, dependency-free type mirrors `AudioFrameResampler`'s recognition
direction role and makes it exhaustively testable with plain float arrays.

**Data Model**: Immutable: the engine sample rate, the target (device) sample rate, and the
target channel count, all validated as positive at construction. No per-call state, so one
instance serves a whole playback session.

**Key Methods**:

- **Resample(samples, sourceSampleRate, targetSampleRate)**: Produces
  `floor(length * target / source)` samples via linear interpolation, clamped to the last input
  sample at the boundary. Equal rates copy the input unchanged, so the identity case introduces no
  error at all. When downsampling, a small Hamming-windowed sinc lowpass filter runs first to
  attenuate above-target-Nyquist energy before decimation.
- **UpmixToChannels(samples, channelCount)**: Replicates each mono sample across every output
  channel, interleaved. A single required channel copies the input unchanged.
- **Convert(samples)**: Composes `Resample` then `UpmixToChannels` into the single operation the
  synthesis pipeline needs for every segment it plays.

**Error Handling**: A non-positive sample rate or channel count throws
`ArgumentOutOfRangeException`. Empty input returns an empty result rather than throwing, since a
pure-silence segment legitimately has no samples to convert.

**Dependencies**: `AudioSubsystem.WindowedSincLowpassFilter` for the downsampling anti-aliasing
lowpass stage, shared with `AudioFrameResampler`'s identical need in the recognition subsystem;
otherwise none beyond the Base Class Library.

**Callers**: `SherpaOnnxSpeechSynthesizer.PlayStreamAsync(...)`.
