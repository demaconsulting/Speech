### SherpaOnnxSpeechRecognizer

This chapter covers the streaming recognizer together with the two units it exists to
coordinate - the recognition-engine seam and the audio-format converter - because none of the
three can be reviewed meaningfully in isolation: the recognizer's whole job is to move audio from
the converter into the engine on the right thread.

**Purpose**: Stream a capture device's audio through format conversion into a recognition engine
and deliver the resulting provisional and final results, without ever performing recognition work
on the audio callback thread.

**Data Model**: Holds the owned `IRecognitionEngine`, the `IAudioCaptureDevice` it streams from,
the owning `IRecognitionModel` (used only to normalize result text), an `AudioFrameResampler`
configured at construction from the device's reported format and the model's declared `AudioFormat.SampleRate`, and
a diagnostics sink. While running it also holds a bounded `Channel<float[]>` of pending capture
blocks and the background consumer `Task` draining it; both are created on start and cleared on
stop, guarded by a lock together with the running and disposed flags. `IsAvailable` is always
`true`, because this type is only ever created after the engine loaded and the device reported
itself available - every unavailable case is represented by `UnavailableSpeechRecognizer`
instead.

The pending-block queue holds 64 blocks and drops the oldest when full. Recognition that has
fallen behind live audio cannot be caught up by queueing more of it, so bounding the backlog
keeps both memory and latency flat instead of letting them grow without limit.

**Key Methods**:

- **Start()**: Creates the queue and consumer task, subscribes to `FrameCaptured`, then starts
  the capture device. Subscribing before starting guarantees no captured block can be raised
  before there is a handler to enqueue it. Starting an already-running recognizer is a no-op.
  Precondition: not disposed. Postcondition: capture is running and results will be raised.
- **Stop()**: Unsubscribes, completes the queue, joins the consumer task, then stops the capture
  device. Because the consumer drains everything already queued before exiting, every result
  derived from audio captured before the call has been delivered when it returns. Stopping a
  recognizer that is not running is a no-op.
- **Dispose()**: Performs the stop sequence (if running) and disposes the owned engine.
  Idempotent.
- **OnFrameCaptured(...)**: Runs on the audio callback thread. Copies the block and enqueues it;
  nothing else.
- **ProcessFrame(...)**: Runs on the consumer thread. Converts the block through
  `AudioFrameResampler`, feeds it to the engine, then polls the engine for results. Each result's
  text is passed through the owning model's `IRecognitionModel.NormalizeText(text, isFinal)`
  before `ResultReceived` raises it, so a model whose raw output needs casing/contraction/
  punctuation restoration (see `UppercaseTranscriptRestorer`) surfaces readable text to every
  consumer instead of the engine's raw output; bounded at 32 results per block so a faulty engine
  cannot livelock the consumer.

**Error Handling**: Every stage is contained. A failure enqueueing a block, converting it,
running inference on it, or delivering a result to a host handler is caught and reported through
`ISpeechDiagnostics`, never rethrown - an exception escaping the frame handler would propagate
into the native PortAudio callback and tear down the audio stream, and a single bad block must
never end the session. A capture device that reported itself available but fails on `Start()` is
the one genuine failure the caller asked for, so it unwinds the partially started pipeline and is
surfaced as `SpeechRecognizerUnavailableException` with the device's exception as
`InnerException`. A device reporting a non-positive rate or channel count falls back to a
pass-through conversion with a warning rather than throwing at composition. Starting after
disposal throws `ObjectDisposedException`.

**Dependencies**: `IRecognitionEngine`, `AudioFrameResampler`, `SpeechRecognitionResult`,
`SpeechRecognitionEvent`, `SpeechRecognizerUnavailableException`, `IRecognitionModel` (from the
ModelManagementSubsystem, for `NormalizeText`), `IAudioCaptureDevice` and
`AudioCaptureFrameEventArgs` from the AudioSubsystem, and `ISpeechDiagnostics` from the
Diagnostics subsystem.

**Callers**: `SpeechRecognizerFactory.Create(...)`.

#### IRecognitionEngine and IRecognitionEngineFactory

**Purpose**: Confine every speech-inference interop call behind one mockable boundary, so the
recognizer's threading, conversion, and result-delivery logic is verifiable with pure managed
fakes - no downloaded model and no platform-specific native binary are ever required in CI. This
mirrors the `IPortAudioApi` seam used for audio interop and the `IModelDownloadClient` seam used
for downloads.

**Data Model**: N/A (interfaces only).

**Key Methods**:

- **IRecognitionEngine.AcceptSamples(ReadOnlySpan&lt;float&gt;)**: Buffers one block of mono
  samples, already at the model's required rate, into the current utterance. Never blocks on
  decoding; an empty block is a no-op.
- **IRecognitionEngine.TryDecode(out SpeechRecognitionResult?)**: Decodes as much buffered audio
  as possible and reports the next result, returning `false` when there is nothing new. Reporting
  "nothing new" instead of an empty result keeps the caller's event stream free of duplicates
  while silence is streaming. Callers may poll until it returns `false`.
- **IRecognitionEngine.Reset()**: Discards a partially decoded utterance.
- **IRecognitionEngineFactory.Create(IRecognitionModel, string installedModelDirectory)**: Loads
  an engine from the model's own declared configuration and required input `AudioFormat`.

**Error Handling**: Implementations are not thread-safe by contract; the recognizer calls them
from exactly one consumer thread. Load failures surface as exceptions from `Create(...)`, which
`SpeechRecognizerFactory` converts into the honest unavailable fallback.

**Dependencies**: `SpeechRecognitionResult`; `IRecognitionModel` from the
ModelManagementSubsystem.

**Callers**: `SherpaOnnxSpeechRecognizer` (engine) and `SpeechRecognizerFactory` (factory).

#### SherpaOnnxRecognitionEngine and SherpaOnnxRecognitionEngineFactory

**Purpose**: Implement the engine seam against the real sherpa-onnx streaming API. These are the
only types in the library that call speech-inference APIs.

**Data Model**: The engine holds one loaded `OnlineRecognizer`, the `OnlineStream` carrying the
current utterance, the declared input sample rate, the text most recently reported as
provisional, and a disposed flag. The factory is stateless and holds no model-specific knowledge
at all - the design makes each model's backing class responsible for its own engine
configuration, so adding a model never requires changing the factory.

**Key Methods**:

- **AcceptSamples(...)**: Pushes the block into the stream at the model-declared sample rate. The managed
  binding takes an array rather than a span, so the block is materialized before crossing the
  interop boundary. When the model has opted into the post-endpoint warm-up-replay feature (see
  below), the same block is also appended to a rolling pre-endpoint sample buffer, trimmed to the
  configured window.
- **TryDecode(...)**: Decodes while the recognizer reports the stream ready, then reads the
  current text. When the recognizer reports an endpoint - and, for a warm-up-replay-enabled
  model, when no post-replay grace period is still counting down - the text is emitted as a final
  result and the stream is reset for the next utterance. The buffered pre-endpoint audio is
  silently replayed into the freshly reset stream only when genuine (non-empty) recognized text
  has been produced at some point since the last reset (`_hasRecognizedTextSinceReset`, checked
  immediately before the reset clears it); otherwise the buffer is simply dropped without replay
  (see "Replay eligibility is gated on genuine recognized text since the last reset" below).
  Otherwise the text is emitted as provisional, suppressed when empty or unchanged since the
  previous call.
- **Reset()**: Resets the stream, clears the remembered provisional text, and (if enabled) clears
  the warm-up buffer, the grace-period counter, and the `_hasRecognizedTextSinceReset` flag.
- **Dispose()**: Releases the stream and then the recognizer that owns it. Safe to call more than
  once.
- **Create(...)**: Asks the model for its configuration, resolved against the installed-files
  directory, and loads an engine at the same model's declared `AudioFormat.SampleRate` and declared
  `PostEndpointWarmupWindowMs`.

**Post-endpoint warm-up replay (Nemotron-only mitigation).** A real, confirmed defect was found
in `SherpaOnnxNemotronStreamingEnRecognitionModel`: its endpoint detector can fire as a false
positive mid-utterance, and the resulting `Reset()` exposes a measured ~550ms encoder warm-up
blackout during which genuinely spoken audio landing in that window is silently lost (reproduced
on two real recordings - full evidence in
`.agent-logs/planning-nemotron-endpoint-word-loss-warmup-replay-fix-9d4b71.md`). Threshold-tuning
the endpoint rules was rejected (it only delays the same failure to a longer pause, per
`.agent-logs/planning-nemotron-endpoint-word-loss-live-failure-6c3f2a.md`). The fix instead
decouples "when reset fires" from "whether audio is lost across it": `IRecognitionModel` declares
an opt-in, default-disabled `PostEndpointWarmupWindowMs` (`0`); `SherpaOnnxRecognitionEngine`
only allocates a rolling sample buffer and maintains grace-period bookkeeping when a model
supplies a positive value, so every other model's behavior and performance are provably
unchanged. On endpoint, the engine resets the stream and then silently replays the buffered
pre-endpoint audio - feeding and decoding it exactly like live audio, but discarding its
`GetResult()` text rather than ever surfacing it as a `SpeechRecognitionResult` - which pre-warms
the same encoder state before genuinely new audio resumes. A second issue found during testing -
the replayed near-silent audio can immediately re-satisfy the trailing-silence endpoint rule
before any genuinely new audio arrives - is handled by a fixed 1.3s grace period of
genuinely-new-audio after every replay, during which a reported endpoint is treated as spurious
and falls through to ordinary provisional handling instead of resetting again.
`SherpaOnnxNemotronStreamingEnRecognitionModel` sets this window to `800`ms (empirically
validated: 600ms is the minimum that reliably recovers the lost words, with no duplication
observed up to 1500ms). The same investigation found that forcing the same window onto
`SherpaOnnxZipformerEnRecognitionModel` caused genuine text duplication (a replayed word's
still-forming onset can be committed as a token by the transducer decoder even though its
`GetResult()` output is suppressed, then duplicated against the same word's genuine live
continuation) - this is a real, model-internal limitation with no known API workaround, which is
exactly why the feature is scoped per-model and opt-in rather than a shared default.
`SherpaOnnxZipformerEnRecognitionModel` deliberately does not override
`PostEndpointWarmupWindowMs` and keeps the disabled `0` default.

**Replay eligibility is gated on genuine recognized text since the last reset (regression fix).**
The first shipped version of this feature replayed the buffered pre-endpoint audio for *every*
endpoint unconditionally, including one that fires on pure/near-silence before any speech has
occurred - a real, ordinary occurrence any time a recording begins with a moment of dead air,
since sherpa-onnx's own `Rule1` endpoint rule fires on trailing silence alone
(`must_contain_nonsilence = false`) with no speech required at all. An independent quality review
(`.agent-logs/quality-nemotron-endpoint-warmup-replay-fix-7a2f91.md`) confirmed this deterministically
corrupts the *first* genuinely recognized segment of a session: replaying near-silent buffered
audio into the freshly reset (encoder-cold) stream can cause the transducer's greedy decoder to
commit a spurious token during the discarded replay-decode pass - only the replay's `GetResult()`
text is discarded, not the decoder's persistent autoregressive hypothesis state, so that stray
token silently prefixes whatever text is later reported for the following, genuinely-spoken
utterance. This was directly observed on a real recording with several seconds of leading silence:
the clean baseline transcript `"Or not to be"` became `"B or not to be"` once the (at-the-time
unconditional) replay feature was enabled.
<br/><br/>
The fix is a per-cycle `_hasRecognizedTextSinceReset` flag: set whenever `GetResult()` produces
non-empty text, cleared on every reset (endpoint-triggered or explicit), and read immediately
before `Reset()` clears it when an endpoint fires. Replay is only attempted when the flag was
`true` at that moment; otherwise the buffered audio is simply dropped and the stream resets exactly
as it would with the feature disabled. This is deliberately a per-cycle gate, not a
session-lifetime "has speech ever occurred" flag: a session-lifetime flag would stay `true`
forever after the first sentence and would therefore still incorrectly permit replay on a later
silence-only endpoint occurring after a pause between sentences later in the same session. The
original mid-utterance word-loss case the feature exists to fix is unaffected: by definition, a
genuine mid-utterance false-positive endpoint only ever occurs after real speech is already in
progress within that cycle, so the flag is already `true` when it fires.

**A related, pre-existing, out-of-scope limitation (disclosed, not fixed by either round of this
feature).** Investigation of the corruption above also found that the *first word* of an
utterance can sometimes still be lost immediately after a silence-only endpoint (for example
`"Be or not to be"` instead of `"To be or not to be"`), when genuine speech begins shortly after
such an endpoint. This is **not** introduced by the warm-up-replay feature or its regression fix:
`TryDecode()`'s `_recognizer.Reset(_stream)` call on the `isEndpoint` branch is unconditional and
always ran regardless of this feature or its configured window, so a silence-only endpoint already
risked a "cold encoder swallows the first word of the next utterance" symptom even with the
feature fully disabled (`PostEndpointWarmupWindowMs == 0`) - confirmed directly by reproducing the
identical symptom with the feature disabled. It is a distinct, structurally different defect (it
loses the *start* of the next utterance after a no-speech-yet reset, rather than corrupting a word
already mid-utterance when a false endpoint fires) and is left as a known limitation for a future
investigation, not addressed here.

**Error Handling**: Construction loads the model into native memory and therefore throws when the
native runtime for the current platform is absent or the model files are unusable; that exception
is what `SpeechRecognizerFactory` converts into the honest unavailable fallback. Operational
members throw `ObjectDisposedException` after disposal.

**Dependencies**: The sherpa-onnx managed API (see *SherpaOnnx Design*); `IRecognitionModel` from
the ModelManagementSubsystem.

**Callers**: `SpeechRecognizerFactory` constructs the factory; the factory constructs the engine;
`SherpaOnnxSpeechRecognizer` uses the engine.

#### AudioFrameResampler

**Purpose**: Convert one block of interleaved, multi-channel capture audio into the mono audio at
the model-declared `AudioFormat.SampleRate` that a recognition engine requires. Keeping this
conversion in a pure,
dependency-free type makes it exhaustively testable with plain float arrays.

**Data Model**: Immutable: the source sample rate, source channel count, and target sample rate,
all validated as positive at construction. No per-block state, so one instance serves a whole
session and is safe to share across threads.

**Key Methods**:

- **Convert(ReadOnlySpan&lt;float&gt;)**: Downmixes then resamples. Downmixing first means the
  interpolation runs over one signal rather than once per channel, which is both cheaper and
  removes any possibility of channels drifting out of alignment. Returns an empty array when the
  input contains no complete frame.
- **DownmixToMono(ReadOnlySpan&lt;float&gt;, int channelCount)**: Averages each frame's channels,
  accumulating in double precision so a high channel count cannot lose low-level detail to
  repeated single-precision rounding. A trailing partial frame is discarded, since it has no
  defined average. A single-channel input is copied unchanged. Averaging values in `[-1.0, 1.0]`
  stays in range, so no clipping step is needed.
- **Resample(ReadOnlySpan&lt;float&gt;, int sourceSampleRate, int targetSampleRate)**: Produces
  `floor(length * target / source)` samples, each the linear blend of the two input samples
  straddling its position, with the final positions clamped to the last input sample. Equal rates
  copy the input unchanged, so the identity case introduces no error at all. When downsampling, a
  small Hamming-windowed sinc lowpass filter runs first to attenuate above-target-Nyquist energy
  before decimation. The output length is computed in 64-bit arithmetic so a long block at a high
  rate cannot overflow the intermediate product.

**Error Handling**: A non-positive sample rate or channel count throws
`ArgumentOutOfRangeException`, because no meaningful conversion exists for it. Empty,
single-sample, and rounds-to-empty conversions all return an empty or clamped result rather than
throwing, since a capture device may legitimately deliver a very short block.

**Dependencies**: `AudioSubsystem.WindowedSincLowpassFilter` for the downsampling anti-aliasing
lowpass stage, shared with `PlaybackAudioResampler`'s identical need in the synthesis subsystem;
otherwise none beyond the Base Class Library.

**Callers**: `SherpaOnnxSpeechRecognizer`.

#### Design Constraints

**Resampler quality is a deliberate, bounded trade-off.** Downmixing remains a straight
arithmetic mean and the upsampling path remains linear interpolation, keeping the implementation
small, reviewable, and dependency-free. The downsampling path now adds a small Hamming-windowed
sinc FIR lowpass filter immediately before decimation, closing the most harmful aliasing case
without turning this unit into a full polyphase DSP subsystem. Each captured block is still
converted independently, so interpolation restarts at every block boundary, but the new filter
removes much of the above-target-Nyquist energy that would otherwise fold into the speech band.
A later phase should revisit the design only if a real model's measured recognition accuracy on
non-native-rate hardware is shown to require something stronger.

**Recognition results are never reported through diagnostics.** Every diagnostic this subsystem
emits is a structural fact - composed, started, stopped, or a named fault - and never includes
recognized text, honoring the `ISpeechDiagnostics` contract that makes it safe for a host to wire
a diagnostics sink to a visible developer log.

**Zero real recognition models ship in this phase.** `SpeechModelCatalog.KnownModels` remains
empty, so the subsystem is proven end to end against a test model rather than against real
speech. This is the same precedented scope boundary Sub-phase 2b used for the model
catalog/contract seam: shipping a production model requires a verified download URL, checksum,
and model-selection decision that are out of scope for infrastructure work. Real
microphone-to-real-text behavior therefore remains unproven outside fakes until a later phase
adds a real model, and a future phase's planning still owes that model.
