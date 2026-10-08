### SherpaOnnxRecognitionEngine

**Purpose**: Implement the core library's `IRecognitionBackend` seam against the real sherpa-onnx
streaming API. This is the only type in this repository that calls speech-recognition inference
APIs. It moved out of the core `Speech` library into this system, together with the concrete
models that construct it, so that the core library carries no sherpa-onnx dependency at all; its
namespace (`DemaConsulting.Speech.RecognitionSubsystem`) is unchanged by the move. The former
`SherpaOnnxRecognitionEngineFactory` was removed rather than moved: the core library's
model-driven `DefaultRecognitionBackendFactory` now asks each model to construct its own backend
through `IRecognitionModel.CreateBackend`, and each recognition model in this system implements
that member by constructing this engine directly.

**Data Model**: Holds one loaded `OnlineRecognizer`, the `OnlineStream` carrying the current
utterance, the declared input sample rate, the text most recently reported as provisional, and a
disposed flag. When the constructing model opts into the post-endpoint warm-up replay feature
(see below), it additionally holds a rolling pre-endpoint sample buffer trimmed to the configured
window, a post-replay grace-period counter, a `_hasRecognizedTextSinceReset` flag, and a
`_hasReplayOnlyHypothesis` flag.

**Key Methods**:

- **SherpaOnnxRecognitionEngine(config, sampleRate, postEndpointWarmupWindowMs = 0)**: Loads the
  recognizer from the supplied `OnlineRecognizerConfig` and creates the first stream. Called only
  from the recognition models' own `CreateBackend` implementations, which pass their own
  `BuildEngineConfig(installedModelDirectory)` result, their declared `AudioFormat.SampleRate`, and
  their declared `PostEndpointWarmupWindowMs`.
- **AcceptSamples(...)**: Pushes the block into the stream at the model-declared sample rate. The
  managed binding takes an array rather than a span, so the block is materialized before crossing
  the interop boundary. When the model has opted into the post-endpoint warm-up-replay feature
  (see below), the same block is also appended to a rolling pre-endpoint sample buffer, trimmed to
  the configured window.
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
- **TryFlush(...)**: The session-end flush (called by `RecognitionSession.StopAsync` immediately
  before `Reset()`). Marks the stream's input finished so the native decoder pads and decodes
  whatever audio is still buffered instead of waiting for future context that will never arrive,
  then reports any remaining text as a final result. This is what lets a push-to-talk release with
  no trailing silence still produce a final result for the word it cut off. Reports nothing when
  the stream's only hypothesis was silently produced by a warm-up replay with no genuinely new
  audio since, so discarded replay text never resurfaces as a flushed result.
- **Reset()**: The session-end reset (called by `RecognitionSession.StopAsync`). Creates a
  replacement stream and disposes the existing one, clears the remembered provisional text, and
  (if enabled) clears the warm-up buffer, the grace-period counter, and the
  `_hasRecognizedTextSinceReset` flag. Recreating the stream - not just calling
  `_recognizer.Reset(_stream)` - is required because that native call only clears the decoder's
  hypothesis: audio already accepted via `AcceptWaveform` but not yet decoded (a streaming
  transducer buffers audio pending future context) survives an in-place reset and would otherwise
  decode into the next session as soon as any audio, even silence, supplied that missing future
  context (a real regression: an abandoned utterance's tail bled into the next `Start()`). The
  replacement is created before the old stream is disposed so a `CreateStream()` failure leaves
  the existing stream intact. This is distinct from the endpoint-triggered reset inside
  `TryDecode()`, which still calls `_recognizer.Reset(_stream)` on the same stream in place,
  because that path is a normal utterance boundary where the buffered pre/post-endpoint audio is
  wanted for the warm-up replay described below.
- **Dispose()**: Releases the stream and then the recognizer that owns it. Safe to call more than
  once.

**Post-endpoint warm-up replay (Nemotron-only mitigation).** A real, confirmed defect was found
in `SherpaOnnxNemotronStreamingEnRecognitionModel`: its endpoint detector can fire as a false
positive mid-utterance, and the resulting `Reset()` exposes a measured ~550ms encoder warm-up
blackout during which genuinely spoken audio landing in that window is silently lost (reproduced
on two real recordings). Threshold-tuning the endpoint rules was rejected, because it only delays
the same failure to a longer pause. The fix instead
decouples "when reset fires" from "whether audio is lost across it": the engine accepts an
opt-in, default-disabled `postEndpointWarmupWindowMs` constructor argument (`0`), supplied by each
model's own `CreateBackend` implementation, and only allocates a rolling sample buffer and
maintains grace-period bookkeeping when a model supplies a positive value, so every other
model's behavior and performance are provably unchanged. On endpoint, the engine resets the
stream and then silently replays the buffered
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
confirmed this deterministically
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

**Error Handling**: The constructor throws `ArgumentOutOfRangeException` for a non-positive
`sampleRate` or a negative `postEndpointWarmupWindowMs`. Construction loads the model into native
memory and therefore throws when the native runtime for the current platform is absent or the
model files are unusable; that exception propagates out of the model's `CreateBackend` and the
core library's `DefaultRecognitionBackendFactory` to `SpeechRecognizerFactory`, which converts it
into the honest unavailable fallback. Operational members throw `ObjectDisposedException` after
disposal.

**Dependencies**: The sherpa-onnx managed API (see *SherpaOnnx Design*); the core library's
`IRecognitionBackend` and `SpeechRecognitionResult` from the *Speech RecognitionSubsystem
Design*.

**Callers**: `SherpaOnnxZipformerEnRecognitionModel` and
`SherpaOnnxNemotronStreamingEnRecognitionModel` construct the engine from their
`CreateBackend` implementations; the core library's `SpeechRecognizerEngine` owns the engine and
`RecognitionSession` drives it.
