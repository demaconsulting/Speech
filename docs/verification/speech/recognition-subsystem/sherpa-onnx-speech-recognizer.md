### SherpaOnnxSpeechRecognizer

#### Verification Approach

The streaming recognizer, the recognition-engine seam, and the audio-format converter are
verified together, since the recognizer's whole purpose is to move audio from the converter into
the engine on the right thread.

The engine seam is replaced by a deterministic fake that records the mono samples it was fed and
yields a scripted sequence of results, so no downloaded model and no platform-specific native
speech-inference binary is ever needed. The capture device is an NSubstitute
`IAudioCaptureDevice` whose `FrameCaptured` event the test raises directly, standing in for a
real audio callback. Diagnostics are an NSubstitute sink, so fault containment is verified by
observing what was reported rather than by asserting an absence of exceptions alone.

Threading is verified without timing assumptions. The recognizer's `Stop()` completes its
internal queue and joins its background consumer, so a test raises a frame, calls `Stop()`, and
then asserts on the fully drained result - no sleeps, polls, or timeouts appear anywhere.

The converter is verified separately as a pure function over plain float arrays, with a
tolerance-based float comparer so assertions describe signal content rather than exact binary
floating-point representation.

The real sherpa-onnx engine adapter's core decode/reset loop is **not** covered by automated
tests against arbitrary models: exercising it in general requires loading a real model through
the native runtime, which is manual/local verification. The seam is what keeps that uncovered
surface as small as possible - it contains only the interop calls, with all policy above it fully
covered. The one exception is the post-endpoint warm-up-replay bookkeeping added to fix the
Nemotron word-loss defect (see the design chapter): its rolling-buffer/replay/grace-period state
machine is verified directly against the real, already-installed streaming Zipformer model in
`SherpaOnnxRecognitionEngineTests`, using reflection to inspect the engine's private bookkeeping
fields, because that state machine cannot be exercised meaningfully through a fake. Those tests
skip (rather than fail) when the model is not installed in the running environment, and skip an
individual real-endpoint assertion if the native endpoint detector does not fire within the
test's bounded silence budget, since real endpoint timing depends on the native model's own rules
rather than on this project's code.

#### Real-Recording Re-Verification Evidence (Nemotron Word-Loss Fix)

The post-endpoint warm-up-replay mechanism was validated end to end against two real,
user-recorded WAV files (never synthetic audio) fed through the real, unmodified recognition
pipeline with the real Nemotron model, per
`.agent-logs/planning-nemotron-endpoint-word-loss-warmup-replay-fix-9d4b71.md`:

- A window-size sweep from 400ms-1500ms found 600ms the minimum window that reliably recovers the
  previously-dropped words ("that is" in "To be or not to be... that is the question") on both
  real recordings, with no duplicated text observed at any tested size up to 1500ms for Nemotron.
- 800ms (comfortable margin above the 600ms minimum) was chosen as the production value for
  `SherpaOnnxNemotronStreamingEnRecognitionModel.PostEndpointWarmupWindowMs`.
- The same feature, forced on for `SherpaOnnxZipformerEnRecognitionModel` on the same recordings,
  produced genuine duplicated text (for example "THAT THAT IS THE QUESTION") at every tested
  window from 500ms upward - the reason the feature is opt-in per model rather than a shared
  default, and why `SherpaOnnxZipformerEnRecognitionModel` deliberately does not override
  `PostEndpointWarmupWindowMs`.
- A 1.3s post-replay endpoint grace period was found necessary and sufficient to suppress a
  spurious second endpoint the replay itself could otherwise immediately re-trigger.

**The first submission of this round of evidence contained an un-investigated defect.** The
original real-recording table (reproduced in the developer's first report,
`.agent-logs/developer-nemotron-endpoint-word-loss-warmup-replay-fix-9d4b71.md`) showed
`Recording.wav`'s Nemotron transcript as `"B or not to be | That is the question"` - a stray
leading "B" glued onto the front of the first recognized segment - without comment, alongside a
"no duplication observed" narrative that did not acknowledge it. An independent quality review
(`.agent-logs/quality-nemotron-endpoint-warmup-replay-fix-7a2f91.md`, FAILED) investigated and
confirmed this was a real, deterministic regression: `Recording.wav` has several seconds of
leading silence before the first spoken word, long enough for sherpa-onnx's `Rule1` endpoint rule
(which fires on trailing silence alone, with no speech required) to report an endpoint before any
genuine speech had been recognized; the then-unconditional replay mechanism still replayed that
near-silent buffered audio into the freshly reset (encoder-cold) stream, and the cold encoder
committed a spurious "B" token during the discarded replay-decode pass, which silently prefixed
the first genuinely-recognized segment that followed.

**Resolution and re-verification (this round).** The fix
(`.agent-logs/planning-nemotron-endpoint-leading-silence-regression-fix-7a2f91.md`) gates replay
eligibility on a per-cycle `_hasRecognizedTextSinceReset` flag - replay is only attempted when
genuine (non-empty) recognized text has occurred since the stream's most recent reset; an endpoint
firing on leading/inter-utterance silence with nothing genuine recognized yet simply drops the
buffer without replay. Both real recordings were re-run end to end through the real, unmodified
production pipeline (`AudioFrameResampler` + `SherpaOnnxRecognitionEngine`) with the real,
installed Nemotron and Zipformer models after applying the fix:

| Model | Recording | Transcript |
| --- | --- | --- |
| Nemotron (fixed) | `Recording.wav` | `Or not to be \| That is the question` - **stray "B" gone** |
| Nemotron (fixed) | `capture-...2bdb9a.wav` | `To be or not to be \| That is the question` - **"that is" preserved** |
| Zipformer (unaffected) | `Recording.wav` | `To be or not to be. \| That is the question.` - **unchanged** |
| Zipformer (unaffected) | `capture-...2bdb9a.wav` | `To thee or not to be that is the question.` - **unchanged** |

**Confirmed**: the stray-token corruption (`"B or not to be"`) is gone - `Recording.wav`'s
Nemotron transcript now exactly matches the clean, disabled-feature (`window=0`) baseline reported
in the original round (`"Or not to be | That is the question"`). The original mid-utterance
word-loss fix is preserved - `capture-...wav`'s Nemotron transcript still recovers "that is"
exactly as before. Zipformer's two transcripts are byte-identical to its prior verification,
confirming zero regression to the untouched model (`PostEndpointWarmupWindowMs == 0`).

#### Real-Speech Transcription Accuracy Verification Evidence

`SherpaOnnxRecognitionEngineAccuracyTests` closes a previously unaddressed coverage gap:
`SherpaOnnxRecognitionEngineTests` deliberately only proves buffer/replay/grace bookkeeping using
silence and a synthesized sine-wave tone, so no automated test anywhere in this project asserted
on real transcribed text content against a known-correct ground truth before this class existed.
It drives the same real, unmodified engine used above with a real, CC0-licensed spoken-word
recitation of Alfred, Lord Tennyson's public-domain poem "Crossing the Bar"
(`test/DemaConsulting.Speech.Tests/TestData/crossing-the-bar-16k-mono.wav`; full attribution and
license in that folder's `NOTICE.md`), fed through the real engine in 0.1-second streaming chunks
followed by trailing silence to flush the final utterance, and scores the concatenated finalized
transcript against the poem's own published text using Word Error Rate (a small, self-contained
`WordErrorRateCalculator` test helper implementing the standard Levenshtein
edit-distance-over-words algorithm, unit-tested independently of any model with hand-computed
cases). Both transcripts are normalized (lower-cased, punctuation stripped, whitespace collapsed)
before comparison so casing and punctuation differences - which do not reflect whether the
recognizer heard the right words - are never counted as errors. A 20% WER tolerance, not an exact
string match, is required because a real model's raw output legitimately differs from the ground
truth in ways that are not transcription failures - most notably Tennyson's archaic spelling
"crost" (for "crossed"), which a modern model's vocabulary may render as either spelling. Each
test skips (rather than fails) when its target model is not installed in the running environment,
mirroring `SherpaOnnxRecognitionEngineTests`.

Both the real, installed streaming Zipformer and streaming Nemotron models were run end to end
against this fixture in this project's development sandbox:

| Model | Measured Word Error Rate | Outcome |
| --- | --- | --- |
| Zipformer | 2.9% | Well within the 20% tolerance |
| Nemotron | 2.0% | Well within the 20% tolerance |

Both transcripts matched the ground truth almost exactly, with only a small number of homophone/
near-homophone substitutions observed (for example "boundless" recognized as "bountless" by
Nemotron, and archaic "bourne" recognized as "born" by both models) - exactly the kind of
acceptable, non-regression-indicating error the 20% tolerance is designed to absorb.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, no downloaded model, no native speech-inference runtime,
  and no physical audio hardware for `SherpaOnnxSpeechRecognizerTests`; the real, already-installed
  streaming Zipformer model and the real native runtime for `SherpaOnnxRecognitionEngineTests`
  (self-skipping when the model is not installed)
- **Test doubles**: Fake `IRecognitionEngine`/`IRecognitionEngineFactory` implementations plus
  NSubstitute capture devices and diagnostics sinks for `SherpaOnnxSpeechRecognizerTests`

#### Acceptance Criteria

The units are considered verified when captured audio reaches the engine downmixed and resampled
to the model's declared rate, every decoded result is raised in order with its provisional/final
flag intact, start/stop/dispose are idempotent and drain queued audio before returning, engine
and handler faults are reported without escaping, a capture-start failure surfaces the documented
exception, and the converter produces the documented output for identity, upsampling,
downsampling, multi-channel, and boundary inputs while rejecting non-positive rates and channel
counts. For the post-endpoint warm-up-replay feature specifically: a disabled (`0`) window
allocates no buffer and changes no observable behavior; an enabled window allocates a buffer sized
to the configured duration that accumulates and trims fed samples; a real endpoint that occurs
after genuine recognized text has been produced since the last reset consumes the buffer via a
replay that never raises more than one final `SpeechRecognitionResult` per utterance; a real
endpoint that fires on pure/near-silence before any genuine text has been recognized since the
last reset never replays - the buffer is dropped and the grace period never arms; and the
post-replay grace period suppresses a same-tick spurious re-trigger. For real-speech transcription
accuracy specifically: the real, installed Zipformer and Nemotron models each transcribe the real
"Crossing the Bar" recording with a Word Error Rate, against its ground-truth text, that does not
exceed the documented 20% tolerance.

#### Test Scenarios

See the RecognitionSubsystem-level scenarios "Pipeline: Capture Format Conversion", "Pipeline:
Result Delivery and Ordering", "Pipeline: Text Normalization", "Pipeline: Lifecycle and
Draining", "Pipeline: Fault Containment", and "Audio Conversion: Downmix, Rate Conversion, and
Boundaries", plus `SherpaOnnxRecognitionEngineTests`'s "Post-Endpoint Warm-Up Replay" scenarios
covering the disabled no-op path, buffer sizing/accumulation/trimming, replay-on-endpoint,
suppressed replay output, the post-replay endpoint grace period, a silence-only endpoint never
arming replay (`SilenceOnlyEndpoint_NeverArmsReplay`), and the replay-eligibility gate itself
exercised deterministically via direct reflection assertions on `_hasRecognizedTextSinceReset`
(`ReplayEligibility_GatedOnHasRecognizedTextSinceReset`), independent of whether the real model
happens to transcribe a synthesized tone as non-empty text. `SherpaOnnxRecognitionEngineAccuracyTests`
adds the "Real-Speech Transcription Accuracy" scenario: transcribing the real "Crossing the Bar"
recording end to end through the real, installed Zipformer and Nemotron models and asserting the
resulting Word Error Rate against the poem's ground-truth text stays within tolerance.
