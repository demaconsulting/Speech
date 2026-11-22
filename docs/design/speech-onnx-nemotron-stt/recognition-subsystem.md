## RecognitionSubsystem Design

![RecognitionSubsystem Structure](NemotronRecognitionSubsystemView.svg)

### Overview

The SpeechOnnxNemotronStt RecognitionSubsystem supplies the real, raw-ONNX-backed implementation of
the core `Speech` library's public `IRecognitionBackend` seam (see *Speech RecognitionSubsystem
Design*) for Nemotron 3.5 ASR streaming. Unlike the sibling `DemaConsulting.Speech.Sherpa`
package, which drives a bundled native inference engine, this subsystem runs the model's three
graphs directly through ONNX Runtime and owns the whole signal path itself in managed code. The
core library's session, engine, resampling, and result-buffering logic remain in the core library
and drive this backend only through `IRecognitionBackend`. It contains the following units:

- **OnnxNemotronRecognitionEngine**: the real `IRecognitionBackend`
- **NemotronFeatureExtractor**: the streaming log-mel front end
- **NemotronEncoder**: the ONNX Runtime cache-aware encoder behind the `INemotronEncoder` seam
- **NemotronRnntGreedyDecoder**: RNN-T greedy search over the `IRnntNetwork` seam
- **NemotronRnntNetwork**: the ONNX Runtime prediction and joint networks behind `IRnntNetwork`
- **NemotronEndpointDetector**: the decode-clock endpoint rules (with the `NemotronEndpointRules`
  record and the `NemotronEndpointReason` enumeration)
- **SilenceRunLimiter**: the stateful streaming leading-silence limiter
- **DitherNoise**: the seeded Gaussian dither source applied after the limiter
- **NemotronEngineOptions**: the record of the engine's tunable constants (documented with the
  engine)

### Interfaces

The subsystem exposes no public API: every type is `internal`. `OnnxNemotronRecognitionEngine`
implements the core library's public `IRecognitionBackend` (`AcceptSamples`, `TryDecode`,
`TryFlush`, `Reset`, `Dispose`) and produces the core library's `SpeechRecognitionResult` values.
It consumes the `Microsoft.ML.OnnxRuntime` managed API (`InferenceSession`, `OrtValue`) and the
sibling `DemaConsulting.Speech.Onnx` system's `OnnxExecutionProviderSelector` (see *SpeechOnnx
Design*) indirectly, through the model that constructs it. It is constructed only by
`OnnxNemotronMultilingualRecognitionModel.CreateBackend` (see *SpeechOnnxNemotronStt
ModelManagementSubsystem Design*).

### Design

The core library's default backend factory holds no engine-specific knowledge: it forwards to the
selected model's `IRecognitionModel.CreateBackend`. The recognition model implements that member
by building the three sessions and constructing the engine, which the core library's
recognition engine owns and passes to each recognition session; the backend is called from one
dedicated worker at a time and is not thread-safe by contract.

The signal path inside the engine is:

1. `SilenceRunLimiter` truncates long quiet runs (see `silence-run-limiter.md`); the engine feeds
   it one 20 ms limiter frame at a time, so each slice is passed or removed as a whole
2. `DitherNoise` adds seeded Gaussian noise (amplitude 1e-5; see `dither-noise.md`) so digital silence never yields an
   exactly-zero log-mel input
3. `NemotronFeatureExtractor` produces 128-band log-mel frames with NeMo's front-end settings
4. The engine groups frames into 56-frame chunks and prepends a 9-frame pre-encode cache (the
   previous chunk's last nine frames) to make a 65-frame encoder input
5. `NemotronEncoder` runs the cache-aware encoder, feeding its cache outputs back as the next
   call's inputs; the real model returns 7 output frames per chunk, 80 ms of input each
6. `NemotronRnntGreedyDecoder` decodes the encoder output one frame at a time into token ids
7. `NemotronEndpointDetector` is told, after every decoded frame, whether the frame held a token,
   and decides when the utterance ends
8. `NemotronVocabulary` detokenizes the tokens (see the ModelManagementSubsystem)

The engine keeps the detector in step with the input clock with two kinds of bookkeeping, both
in absolute feature-frame positions:

- **Removed silence marks**: whenever the limiter removes quiet samples, the engine records a mark
  of that many samples at the position of the next feature frame to be emitted (consecutive marks
  at one position are merged). Before decoding an encoder frame, every mark at or before the
  frame's first feature position is credited to the detector with `AdvanceSilence`; after a chunk
  and after each pump, the marks up to the decoded position are credited too, so a silent stream
  with no new frames still reaches an endpoint. A 1.5 s pause thus counts as about 400 ms of
  decoded blank frames plus about 1.1 s of credited removed silence.
- **Padding ranges**: whenever the engine feeds synthetic silence (dithered digital silence
  straight into the extractor, bypassing the limiter), it opens a padding range at the current
  position and closes it when real audio next arrives (a flush tail stays open). Padding often
  starts inside an encoder frame (one frame covers 8 feature rows), so the engine counts the rows
  of each frame that lie in padding ranges: a frame with only padding rows is reported with
  `AdvancePaddingFrame` (a token on it still counts as speech, its duration advances neither
  endpoint clock), and a mixed frame is reported with `AdvanceFrame` for the duration of its real
  rows only. Removed-silence marks that fall inside a frame's rows are credited after that frame:
  its single token decision cannot be split, and tokens lag the audio, so a token decoded on the
  frame holding the boundary belongs to the speech before the removed silence.

Padding is applied in two places, and is never all-zero log-mel rows: when a quiet run after
speech reaches the limiter's 400 ms cap, the partial chunk is completed (no further audio will
arrive to fill it); and at flush, the partial chunk is completed and `FlushTailChunks` (1) whole
chunks are added so the encoder's look-ahead releases the last words. The flush tail is decoded
once per stretch of real audio.

After every credit and every decoded frame the engine asks the detector for a decision. With no
tokens in the utterance, any rule only restarts the detector (silence alone never produces text).
The two trailing-silence rules report the utterance as final, clear its tokens, and restart the
detector. The maximum-length rule finalizes the tokens before the last word-start token
(`NemotronVocabulary.StartsWord`) and carries that last word into the next utterance, restarting
the detector with speech already present. None of these resets the encoder, the decoder, or the
extractor: only `Reset()` (a session reset) returns the whole pipeline to the start-of-stream
state. Because the model state is kept, closing-punctuation tokens that would open a segment are
dropped from the text (they belong to the sentence just finalized) and do not count as speech.

All tuning constants live in the internal `NemotronEngineOptions` record: the constants
`ChunkFrames` 56 and `PreEncodeFrames` 9, and the parameters `DitherAmplitude` 1e-5, `DitherSeed`,
`FlushTailChunks` 1, `BlankPenalty` 1.0 (`NemotronRnntNetwork.DefaultBlankPenalty`), `Endpoint`
(the `NemotronEndpointRules`, default 2400 ms, 1200 ms, and 20000 ms), and `Limiter` (the limiter
options). The engine rejects a negative `FlushTailChunks` and the detector a negative rule
duration with `ArgumentOutOfRangeException`; the model validates `BlankPenalty` (finite, not
negative) before it reads any file.

Constructing the sessions loads the model into ONNX Runtime, so unusable model files or an
unavailable execution provider surface as an exception from `CreateBackend`. The core library's
`SpeechRecognizerFactory` catches that exception and degrades it to the honest unavailable
recognizer.
