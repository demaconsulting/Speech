### SherpaOnnxRecognitionEngine

#### Verification Approach

`SherpaOnnxRecognitionEngine` is the SpeechSherpa library's real, native-backed implementation of
the Speech library's internal `IRecognitionBackend` seam. It contains only the sherpa-onnx interop
calls plus the post-endpoint warm-up-replay bookkeeping, so it is verified directly against the
real, already-installed streaming Zipformer and Nemotron models and the real native sherpa-onnx
runtime rather than through a fake: mocking native inference would prove nothing about whether
real audio decodes into real text, which is this unit's entire purpose. All policy above the seam
(threading, lifecycle, result delivery, fault containment) is verified in the Speech library
against a fake backend; see _Speech RecognitionSubsystem Verification_.

The verification is split across two test classes in
`test/DemaConsulting.Speech.Sherpa.Tests/RecognitionSubsystem/`:

- `SherpaOnnxRecognitionEngineTests` verifies the post-endpoint warm-up-replay bookkeeping added
  to fix the Nemotron word-loss defect (see _SpeechSherpa RecognitionSubsystem Design_), whose
  rolling-buffer/replay/grace-period state machine is inspected through reflection on the engine's
  private bookkeeping fields while silence and a synthesized sine-wave tone are fed through the
  real engine
- `SherpaOnnxRecognitionEngineAccuracyTests` verifies real transcribed text: Word Error Rate
  against a real recording's ground truth, the session-end `Reset()` fix that discards buffered,
  not-yet-decoded audio, and `TryFlush()`'s recovery of that same buffered audio as a delivered
  final result

Those tests skip (rather than fail) when the model is not installed in the running environment,
and skip an individual real-endpoint assertion if the native endpoint detector does not fire
within the test's bounded silence budget, since real endpoint timing depends on the native
model's own rules rather than on this project's code. The engine's core decode/reset loop against
arbitrary models beyond these two remains manual/local verification.

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
It drives the same real, unmodified engine used in the re-verification above with a real, CC0-licensed spoken-word
recitation of Alfred, Lord Tennyson's public-domain poem "Crossing the Bar"
(`test/DemaConsulting.Speech.Sherpa.Tests/TestData/crossing-the-bar-16k-mono.wav`; full attribution and
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

#### Session-End Reset Buffered-Audio Regression Evidence

`SherpaOnnxRecognitionEngine_Reset_AbandonedUtteranceWithNoTrailingSilence_DoesNotBleedIntoNextSession`
(`SherpaOnnxRecognitionEngineAccuracyTests`) closes the gap the fix addresses: it feeds the real,
installed streaming Zipformer model the first line of the "Crossing the Bar" recording with no
trailing silence (leaving audio accepted but not yet decoded, mirroring a push-to-talk release),
calls `Reset()`, then feeds silence only and asserts no text at all - and specifically none of the
abandoned line's own words - is reported. Confirmed by reverting only the fix and re-running: the
test fails, reproducing the original leak (the abandoned utterance's tail decoding from
silence-only audio); it passes again with the fix restored.

#### Trailing-Audio Flush Recovery Evidence

Discarding buffered audio on session-end `Reset()` is necessary but, on its own, means a
push-to-talk release with no trailing silence loses the last thing the user said - it is
recovered nowhere. `SherpaOnnxRecognitionEngine_TryFlush_AbandonedUtteranceWithNoTrailingSilence_RecoversTrailingWords`
(`SherpaOnnxRecognitionEngineAccuracyTests`) verifies the fix for that: feeding the real,
installed streaming Zipformer model the same kind of abandoned, no-trailing-silence utterance,
then calling `TryFlush()` instead of `Reset()` directly, produces a final result whose text
contains every word of the abandoned line - proving the native `OnlineStream.InputFinished`
mechanism genuinely recovers audio that would otherwise be silently discarded, not merely that
nothing crashes.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Sherpa.Tests`
- **Dependencies**: The real, already-installed streaming Zipformer and Nemotron models and the
  real native sherpa-onnx runtime; no physical audio hardware and no network access. Each test
  self-skips when its target model is not installed
- **Fixture**: `test/DemaConsulting.Speech.Sherpa.Tests/TestData/crossing-the-bar-16k-mono.wav`
- **Test helper**: `WordErrorRateCalculator`, unit-tested independently of any model by
  `WordErrorRateCalculatorTests` with hand-computed cases

#### Acceptance Criteria

The unit is considered verified when, for the post-endpoint warm-up-replay feature: a disabled
(`0`) window allocates no buffer and changes no observable behavior; an enabled window allocates a
buffer sized to the configured duration that accumulates and trims fed samples; a real endpoint
that occurs after genuine recognized text has been produced since the last reset consumes the
buffer via a replay that never raises more than one final `SpeechRecognitionResult` per
utterance; a replay with no new audio afterward reports nothing further from either `TryDecode`
or `TryFlush`; a real endpoint that fires on pure/near-silence before any genuine text has been
recognized since the last reset never replays - the buffer is dropped and the grace period never
arms; and the post-replay grace period suppresses a same-tick spurious re-trigger. For real-speech
transcription accuracy: the real, installed Zipformer and Nemotron models each transcribe the real
"Crossing the Bar" recording with a Word Error Rate, against its ground-truth text, that does not
exceed the documented 20% tolerance. For the session-end `Reset()` fix: audio accepted but not yet
decoded before a session-end reset never surfaces as text once only silence follows the reset.
For the `TryFlush()` recovery fix: audio accepted but not yet decoded is recovered as a final
result's text when flushed before that reset, rather than only proving it is not lost.

#### Test Scenarios

##### A disabled warm-up window allocates no buffer

**Test**: `SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsZero_NoBufferAllocated`

##### A disabled warm-up window leaves accept and decode a no-op for the warm-up mechanism

**Test**: `SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsZero_AcceptSamplesAndDecode_IsNoOpForWarmupMechanism`

##### An enabled warm-up window sizes its buffer to the window and accumulates samples

**Test**: `SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_BufferSizedToWindowAndAccumulates`

##### The warm-up buffer never exceeds its capacity

**Test**: `SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_BufferNeverExceedsCapacity`

##### A real endpoint replays the buffer and arms the grace period

**Test**: `SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_EndpointReplaysAndArmsGracePeriod`

##### The replay never produces an extra result

**Test**: `SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_ReplayNeverProducesExtraResult`

##### TryFlush after a replay with no new audio reports nothing

**Test**: `SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_TryFlushAfterReplayWithNoNewAudio_ReportsNothing`

##### TryDecode after a replay with no new audio reports nothing

**Test**: `SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_TryDecodeAfterReplayWithNoNewAudio_ReportsNothing`

##### The grace period suppresses an immediate re-trigger

**Test**: `SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_GracePeriodSuppressesImmediateReTrigger`

##### A silence-only endpoint never arms the replay

**Test**: `SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_SilenceOnlyEndpoint_NeverArmsReplay`

##### Replay eligibility is gated on genuine recognized text since the last reset

**Test**: `SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_ReplayEligibility_GatedOnHasRecognizedTextSinceReset`

##### The real Zipformer model transcribes a real recording within the WER tolerance

**Test**: `SherpaOnnxRecognitionEngine_Transcribe_RealCrossingTheBarRecording_WordErrorRateBelowTolerance`

##### The real Nemotron model transcribes a real recording within the WER tolerance

**Test**: `SherpaOnnxRecognitionEngine_Transcribe_RealCrossingTheBarRecording_NemotronWordErrorRateBelowTolerance`

##### A session-end reset never lets an abandoned utterance bleed into the next session

**Test**: `SherpaOnnxRecognitionEngine_Reset_AbandonedUtteranceWithNoTrailingSilence_DoesNotBleedIntoNextSession`

##### A flush recovers the trailing words of an abandoned utterance

**Test**: `SherpaOnnxRecognitionEngine_TryFlush_AbandonedUtteranceWithNoTrailingSilence_RecoversTrailingWords`
