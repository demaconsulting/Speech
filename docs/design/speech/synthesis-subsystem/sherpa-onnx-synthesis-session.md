### SherpaOnnxSynthesisSession

This chapter covers the real session implementation together with the units it exists to
coordinate - the Layer 2 rendering strategy, the sentence chunker, the synthesis-backend seam,
the dedicated-worker cancellation policy, and the playback-format converter - because none can be
reviewed meaningfully in isolation: the session's whole job is to move text through rendering and
chunking, into the backend (via a dedicated worker), and the resulting audio through resampling
onto its bound playback device.

**Purpose**: Chunk, render, and synthesize text into speech through a synthesis backend, bound to
exactly one playback device for the session's entire life, without ever performing synthesis or
inference work on the caller's UI thread, and without pipelining synthesis ahead of playback
across an unbounded queue.

**Data Model**: Holds the owned `ISynthesisBackend`, the `IAudioPlaybackDevice` it plays through,
the `ISynthesisModel` supplying `NormalizeText`/`CapabilityProfile`/`ResolveSpeakerId`, an
optional `parameterValues` bag (the session's selected voice/tunable-parameter values, forwarded
unchanged from `SherpaOnnxSpeechSynthesizerEngine`), a diagnostics sink, a release callback for
the engine's exclusivity lease, and a `_syncRoot` lock guarding its `SynthesisSessionState`, its
current operation's `CancellationTokenSource`, its fault (if any), and disposal/lease-released
flags. `IsAvailable` is `true` unless disposed or `Faulted`.

Unlike the former streaming synthesizer's pending-segment channel, this session does not
pipeline synthesis ahead of playback: each segment is synthesized, then (for `SpeakAsync`)
immediately written to the playback device, before the next segment's synthesis begins. This
keeps the per-call state machine simple - exactly one backend call in flight at a time - while
still overlapping this call's own synthesis-then-playback work normally, since playback of one
segment proceeds without the caller waiting for the whole plan up front.

`DrainPollInterval` (15ms) and `DrainTailMargin` (40ms) are fixed constants governing
`WaitForPlaybackDrainAsync`'s post-loop wait in `GenerateAndOptionallyPlayAsync` (see below):
short enough that a `SpeakAsync` call returns promptly once the hardware is genuinely done, long
enough to avoid busy-spinning the thread pool re-reading a value only a real-time audio callback
thread can change.

**Key Methods**:

- **SpeakAsync(text, cancellationToken)** / **SynthesizeAsync(text, cancellationToken)**: Both
  validate `text` eagerly, then delegate to the shared `RunOperationAsync(text,
  playAfterSynthesis, cancellationToken)`, which validates the overlap rule and current state
  under the lock, transitions `Starting` → `Running`, runs
  `GenerateAndOptionallyPlayAsync`, then transitions `Stopping` → `Stopped` on success or
  cancellation, or to `Faulted` (reporting the fault via diagnostics) on any other exception. A
  second call while an operation is already `Starting`/`Running`/`Stopping` throws
  `InvalidOperationException`; a call while `Faulted` throws `SynthesisSessionFaultedException`
  wrapping the original fault; a call after disposal throws `ObjectDisposedException`.
- **StopAsync(cancellationToken)**: Cancels the current operation's `CancellationTokenSource`
  under the lock, if one exists, then awaits that same tracked operation task (abandoning the
  wait, but not the operation itself, past the dedicated worker's abandon timeout) so the
  returned task completes only once the in-flight operation has genuinely stopped; a safe no-op
  otherwise. The cancelled operation still unwinds through the normal `Stopping` → `Stopped`
  path, not `Faulted`.
- **GenerateAndOptionallyPlayAsync(text, playAfterSynthesis, cancellationToken)**: Normalizes the
  text via `_model.NormalizeText`, parses it via `AudioTagParser.Parse`, renders it via
  `_model.CapabilityProfile.Render(spans, _model)` into a `SpeechPlan`, then synthesizes each
  `SpeechSegment` in turn via `GenerateSegmentAsync`. When `playAfterSynthesis` is `false`
  (`SynthesizeAsync`), simply collects every segment's `SynthesizedSpeech` into the returned list.
  When `true` (`SpeakAsync`), starts the playback device, builds one `PlaybackAudioResampler` for
  the call, writes each segment's pre-silence/resampled audio/post-silence as it is produced, and
  once every segment has been written, awaits `WaitForPlaybackDrainAsync` before a `finally` block
  stops the device - reporting (but not rethrowing) a failure to stop, so the device is never left
  running regardless of whether the loop, the drain wait, or neither completed, faulted, or was
  cancelled.
- **GenerateSegmentAsync(segment, cancellationToken)**: An empty-text segment (a rendered pause)
  skips the backend entirely and returns a pure-silence `SynthesizedSpeech` built directly from
  the segment's declared silence durations. Otherwise resolves speed/volume overrides against the
  model's declared numeric parameters via `SpeechParameterConventions`, resolves the speaker id
  for this session's selected voice by calling `_model.ResolveSpeakerId(_parameterValues)` once
  per segment (cheap and pure - re-evaluated fresh each call rather than cached for the whole
  session; deliberately independent of the per-segment `ParameterOverrides` speed/volume
  mechanism, which is Natural Language Audio Tag-scoped and transient, not a fit for a
  session-level voice selection), and calls `_backend.Generate(text, speedRatio, speakerId)`
  through `DedicatedWorker.Run` - so a non-cooperative native call is bounded by the
  cooperative-cancel-then-abandon policy rather than awaited indefinitely - applying any volume
  override as post-hoc amplitude scaling (sherpa-onnx's offline TTS API has no native gain input),
  clamped to `[-1.0, 1.0]`.
- **WaitForPlaybackDrainAsync(cancellationToken)**: Polls `_device.PendingSampleCount` every
  `DrainPollInterval` (15ms) until it reaches zero - i.e. until the playback hardware has
  genuinely rendered every sample this operation wrote, not merely until every segment was handed
  off - then applies one further `DrainTailMargin` (40ms) wait before returning, to absorb any
  residual internal host buffering (e.g. PortAudio's own ring buffer) that has already left the
  managed queue but has not yet actually reached the speakers. Honors `cancellationToken`: a
  cancellation during either wait ends it immediately via `Task.Delay`'s own cancellation.
- **DisposeAsync()**: Cancels any in-flight operation's `CancellationTokenSource`, transitions
  `Disposing` → `Disposed`, and releases the owning engine's exclusivity lease exactly once (via
  the constructor-supplied release callback). Idempotent.

**Error Handling**: A fault raised synthesizing or playing a segment transitions the session to
`Faulted` and is reported through `ISpeechDiagnostics` before being rethrown to the caller of the
operation that faulted; every subsequent `SpeakAsync`/`SynthesizeAsync` call then throws
`SynthesisSessionFaultedException` wrapping that original fault, until the session is disposed. A
playback-device write failure still stops the device via the `finally` block before propagating.
A cancellation while `WaitForPlaybackDrainAsync` is polling ends the wait immediately via
`Task.Delay`'s own cancellation, while `finally` still stops the device; the operation unwinds to
`Stopped`, not `Faulted`, since cancellation is an ordinary, successful outcome. An unavailable
playback device throws promptly rather than hanging. Calling an operational member after
disposal throws `ObjectDisposedException`; calling one while another is already in flight on the
same session throws `InvalidOperationException`.

**Dependencies**: `ISynthesisBackend`, `SpeechPlan`, `SpeechSegment`, `SpeechParameterConventions`,
`PlaybackAudioResampler`, `SynthesizedSpeech`, `SynthesisSessionState`,
`SessionStateChangedEventArgs`, `SynthesisSessionFaultedException`, `DedicatedWorker`,
`SpeechSynthesizerUnavailableException`, `AudioTagParser` from this subsystem's unchanged units,
`IAudioPlaybackDevice` from the AudioSubsystem, and `ISpeechDiagnostics` from the Diagnostics
subsystem.

**Callers**: `SherpaOnnxSpeechSynthesizerEngine.CreateSessionAsync(device, cancellationToken)`.

#### ISynthesisBackend and ISynthesisBackendFactory

**Purpose**: Confine every speech-synthesis interop call behind one mockable boundary, so the
session's chunking, Layer 2 rendering, per-segment synthesis, and fault-containment logic is
verifiable with pure managed fakes - no downloaded model and no platform-specific native binary
are ever required in CI. This mirrors the `IRecognitionBackend`/`IRecognitionBackendFactory` seam
used for recognition. Renamed from `ISynthesisEngine`/`ISynthesisEngineFactory` so the "engine"
vocabulary is reserved for the public Layer 3 `ISpeechSynthesizerEngine` contract; members are
unchanged by the rename.

**Data Model**: N/A (interfaces only).

**Key Methods**:

- **ISynthesisBackend.SampleRate**: The fixed rate this backend's `Generate(...)` output is
  produced at.
- **ISynthesisBackend.Generate(text, speed, speakerId)**: Synthesizes one chunk of text at the
  given speed multiplier and speaker id, returning an `EngineAudio` (samples plus the rate they
  were produced at).
- **ISynthesisBackendFactory.Create(ISynthesisModel, string installedModelDirectory)**: Loads a
  backend from the model's own declared configuration.

**Error Handling**: Implementations are not thread-safe by contract; a session calls `Generate`
from exactly one `DedicatedWorker` call at a time. Load failures surface as exceptions from
`Create(...)`, which `SpeechSynthesizerFactory` converts into the honest unavailable fallback.

**Dependencies**: `EngineAudio`; `ISynthesisModel` from the ModelManagementSubsystem.

**Callers**: `SherpaOnnxSynthesisSession` (backend) and `SpeechSynthesizerFactory`/
`SherpaOnnxSpeechSynthesizerEngine` (factory).

#### SherpaOnnxSynthesisEngine and SherpaOnnxSynthesisEngineFactory

**Purpose**: Implement the backend seam against the real sherpa-onnx offline TTS API, reusing the
already-referenced `org.k2fsa.sherpa.onnx` package. These are the only types in this subsystem
that call speech-synthesis inference APIs. Class names are unchanged by the
`ISynthesisEngine`→`ISynthesisBackend` interface rename - only the interfaces they implement were
renamed.

**Data Model**: The engine holds one loaded `OfflineTts`, its declared `SampleRate`, and a
disposed flag. The factory is stateless and holds no model-specific knowledge at all - the design
makes each model's backing class responsible for its own engine configuration, so adding a
synthesis model never requires changing the factory.

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

**Dependencies**: The sherpa-onnx managed API (see *SherpaOnnx Design*); `ISynthesisModel` from
the ModelManagementSubsystem.

**Callers**: `SpeechSynthesizerFactory` constructs the factory; the factory constructs the
engine; `SherpaOnnxSpeechSynthesizerEngine` owns the engine and passes it to each
`SherpaOnnxSynthesisSession` it creates.

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

**Key Methods**:

- **Render(spans, model)**: For each `TaggedTextSpanKind.Tag` span: if the tag is a pause, always
  renders a silence-only `SpeechSegment` (300ms short / 900ms long) regardless of
  `model.AudioTagSupport`. Otherwise, if `model.AudioTagSupport` is `Native`, passes the tag
  through as its canonical bracket text so the model's own inference sees the control token. If
  `ParameterMapped`, consults `SpeechParameterConventions` for a matching numeric parameter on
  `model.Parameters` and attaches it as an override on the surrounding segment when one exists,
  otherwise strips the tag. If `None`, strips every non-pause tag. For each
  `TaggedTextSpanKind.PlainText` span, delegates to `SentenceChunker.ChunkWithMetadata(...)` and
  emits one `SpeechSegment` per resulting chunk, setting that segment's `PostSilenceMs` to a new
  `EllipsisPauseMilliseconds` constant (500ms) when the chunk's `EndsWithEllipsis` flag is set, or
  `0` otherwise - a chunk-boundary-triggered pause distinct from, and never reusing, the
  tag-triggered short/long pause durations above.

**Error Handling**: Rejects a null `spans` or `model` with `ArgumentNullException`. Every other
input - any tag, any support level, any parameter set - is handled without throwing, per this
library's "never worse than plain narration" guarantee extended to Layer 2.

**Dependencies**: `TaggedTextSpan`, `TaggedTextSpanKind`, `NaturalLanguageAudioTagKind` from this
subsystem's unchanged units; `SentenceChunker`; `SpeechParameterConventions`; `SpeechSegment`,
`SpeechPlan`; `ISpeechModel`, `SpeechModelAudioTagSupport`, `NumericParameter` from the
ModelManagementSubsystem.

**Callers**: `SherpaOnnxSynthesisSession.GenerateAndOptionallyPlayAsync(...)` via
`ISynthesisModel.CapabilityProfile`.

#### SentenceChunker

**Purpose**: Split synthesis-ready plain text into sentence/clause-sized chunks so chunked
synthesis has natural-sounding boundaries and no chunk exceeds a length a synthesis call can
reasonably handle.

**Data Model**: A stateless static class; `Chunk(text, maxLength)`/`ChunkWithMetadata(text,
maxLength)` take no configuration beyond their two arguments. `ChunkWithMetadata` returns a
`SentenceChunk` record struct per chunk (`Text`, `EndsWithEllipsis`); `Chunk` is a pure projection
of `ChunkWithMetadata`'s chunk text.

**Key Methods**:

- **Chunk(text, maxLength)** / **ChunkWithMetadata(text, maxLength)**: Splits on primary
  sentence-ending punctuation (`.`, `!`, `?`) first, then **unconditionally** splits every
  resulting piece further on secondary clause punctuation (`,`, `;`, `:`) - not only when the
  piece is still over `maxLength` - so every clause becomes its own chunk. A punctuation
  character embedded in a numeral is never treated as a boundary at either pass: a digit both
  immediately before and after (e.g. the `:` in `"12:30"`, the `,` in `"1,000"`, or a decimal
  point such as `"0.5"`) is always kept attached, and a decimal point specifically is also kept
  attached when a digit follows immediately but none precedes, as long as the character before it
  (if any) is not a letter - e.g. a leading fraction such as `".5"`, `"$.99"`, or `".5 units"` at
  start-of-text/after whitespace/a sign/a currency symbol. A genuine sentence-ending period
  directly followed by a digit after a letter (e.g. `"Wait.5 more."`) still splits normally, since
  the numeral exception only applies when no letter precedes the period. A piece still longer
  than `maxLength` after both punctuation passes is split on a whitespace budget; a single word
  that alone exceeds `maxLength` is returned whole, never split mid-word. Any resulting piece
  whose trimmed text contains no letter or digit at all (just punctuation and/or whitespace, e.g.
  a lone comma or a whitespace-spaced ellipsis like `". . ."`) is then merged onto the end of the
  immediately preceding non-empty chunk (joined by a single space), or dropped if there is no
  preceding chunk. `ChunkWithMetadata` additionally flags a chunk whose final text ends in three
  or more consecutive `.` characters (with or without interspersed whitespace) as
  `EndsWithEllipsis`. These numeral exceptions assume English/US-style numeral punctuation (`,`
  as thousands separator, `.` as decimal point); locales that swap the two roles (e.g.
  `"1.000,5"`) are not specially handled - every model in this library's current catalog is
  English-only, so this is not currently a defect, but a future non-English model would need
  these rules adjusted rather than assuming they generalize as-is.

**Error Handling**: Rejects a non-positive `maxLength` with `ArgumentOutOfRangeException` and a
null `text` with `ArgumentNullException`, since neither describes a meaningful chunking request.
Empty and whitespace-only text return no chunks rather than throwing.

**Dependencies**: None beyond the Base Class Library.

**Callers**: `DefaultModelCapabilityProfile.Render(...)`.

#### PlaybackAudioResampler

**Purpose**: Convert one synthesized segment's mono audio, produced at a synthesis backend's
fixed rate, into the format a playback device requires - its resolved rate and channel count.
Keeping this conversion in a pure, dependency-free type mirrors `AudioFrameResampler`'s
recognition direction role and makes it exhaustively testable with plain float arrays.

**Data Model**: Immutable: the backend sample rate, the target (device) sample rate, and the
target channel count, all validated as positive at construction. No per-call state, so one
instance serves a whole operation.

**Key Methods**:

- **Resample(samples, sourceSampleRate, targetSampleRate)**: Produces
  `floor(length * target / source)` samples via linear interpolation, clamped to the last input
  sample at the boundary. Equal rates copy the input unchanged, so the identity case introduces no
  error at all. When downsampling, a small Hamming-windowed sinc lowpass filter runs first to
  attenuate above-target-Nyquist energy before decimation.
- **UpmixToChannels(samples, channelCount)**: Replicates each mono sample across every output
  channel, interleaved. A single required channel copies the input unchanged.
- **Convert(samples)**: Composes `Resample` then `UpmixToChannels` into the single operation the
  session needs for every segment it plays. When the target channel count is 1,
  `UpmixToChannels` is skipped entirely and the resampled result is returned directly, since
  `UpmixToChannels`'s own single-channel case would only make a redundant copy of an array
  `Convert` already owns exclusively.

**Error Handling**: A non-positive sample rate or channel count throws
`ArgumentOutOfRangeException`. Empty input returns an empty result rather than throwing, since a
pure-silence segment legitimately has no samples to convert.

**Dependencies**: `AudioSubsystem.WindowedSincLowpassFilter` for the downsampling anti-aliasing
lowpass stage, shared with `AudioFrameResampler`'s identical need in the recognition subsystem;
otherwise none beyond the Base Class Library.

**Callers**: `SherpaOnnxSynthesisSession.PlaySegment(...)` (via `GenerateAndOptionallyPlayAsync`).

#### DedicatedWorker

**Purpose**: Run one native synthesis call on a dedicated, long-running background thread,
applying a cooperative-cancel-then-abandon policy so a non-cooperative native call cannot hang a
caller's awaited task indefinitely. `SynthesisSubsystem`'s own duplicated copy of
`RecognitionSubsystem`'s identically-shaped internal utility.

**Data Model**: A stateless static class. `Run(delegate, cancellationToken, diagnostics,
diagnosticsCategory, abandonTimeout = null)` takes no instance state; `abandonTimeout` defaults
to `DefaultAbandonTimeout` (2 seconds) when omitted/`null`, and is injectable for deterministic
test coverage of the abandon path.

**Key Methods**:

- **Run(delegate, cancellationToken, diagnostics, diagnosticsCategory, abandonTimeout)**: Starts
  `delegate` on a `TaskCreationOptions.LongRunning` task. On normal completion, returns its
  result. On cancellation, awaits the task for up to `abandonTimeout`; if it completes
  cooperatively within that window, the cancellation propagates normally. If it does not, reports
  a `Warning` diagnostic, detaches the still-running task (attaching a continuation that observes,
  rather than propagates, whatever it eventually produces, so it can never become an
  unobserved-exception crash), and throws `OperationCanceledException` to its own caller anyway -
  the caller must not be held open indefinitely by a native call that refuses to stop.

**Error Handling**: A genuine (non-cancellation) fault from `delegate` propagates normally from
the awaited task. The abandon path above is the sole case where this method returns/throws before
`delegate` has actually finished running.

**Dependencies**: `ISpeechDiagnostics` from the Diagnostics subsystem.

**Callers**: `SherpaOnnxSynthesisSession.GenerateSegmentAsync(...)` for every native
`ISynthesisBackend.Generate(...)` call, and `SpeechSynthesizerFactory.LoadAsync(...)` for the
blocking native model load.
