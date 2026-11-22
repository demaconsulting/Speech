### OnnxNemotronRecognitionEngine

**Purpose**: Implement the core library's `IRecognitionBackend` seam against the Nemotron
streaming ONNX model, run directly through ONNX Runtime with no bundled native inference engine.
It owns the whole streaming pipeline - leading-silence limiting, dither, feature extraction,
chunked cache-aware encoding, frame-by-frame greedy decoding, detokenization, silence padding, and
decode-clock endpointing - and reports provisional and final text. This package's
`NemotronEncoder` and `NemotronRnntNetwork` are the only types that run ONNX Runtime inference;
they are documented as their own units (`nemotron-encoder.md` and `nemotron-rnnt-network.md`).

**Data Model**: Holds an `INemotronEncoder`, a `NemotronRnntGreedyDecoder`, a
`NemotronVocabulary`, a `NemotronEngineOptions`, a `SilenceRunLimiter`, a `DitherNoise`, a
`NemotronFeatureExtractor`, and a `NemotronEndpointDetector`; a reusable limiter-output list; one
160-sample hop of silence re-dithered for every padding hop; a 65-frame encoder input array
(`EncoderFrames = PreEncodeFrames + ChunkFrames`, 128 floats per frame); the 9-frame pre-encode
carry array; the token ids of the utterance in progress; a queue of pending
`SpeechRecognitionResult`s; the last provisional text; the removed-silence marks (absolute
feature-frame position and sample count, oldest first); the padding ranges (absolute
feature-frame start inclusive and end exclusive, the last one open while padding is the latest
input); the limiter slice length (one limiter frame, 320 samples) and the quiet-cap length after
which a partial chunk is padded (`MaxQuietFrames * FrameSamples`, 400 ms); the number of feature
frames handed to the encoder since the last reset; and flags (`_paddingOpen`; `_speechSeen`, loud
audio seen since the last reset; `_realAudioSinceTail`, real audio fed since the last flush tail;
`_disposed`). `NemotronEngineOptions` (an internal record) holds the constants `ChunkFrames` 56
and `PreEncodeFrames` 9 and the parameters `DitherAmplitude` 1e-5, `DitherSeed`,
`FlushTailChunks` 1, `BlankPenalty` 1.0 (`NemotronRnntNetwork.DefaultBlankPenalty`, passed to the
network by the model), `Endpoint` (`NemotronEndpointRules`, `null` for 2.4 s, 1.2 s, and 20 s), and
`Limiter` (`SilenceRunLimiterOptions`, `null` for the defaults). Invariant: the feature position
(frames handed to the encoder plus frames queued in the extractor) only grows until `Reset()`, so
marks and padding ranges are consumed in order.

**Key Methods**:

- **OnnxNemotronRecognitionEngine(encoder, decoder, vocabulary, options)**: throws
  `ArgumentNullException` for a null required argument and `ArgumentOutOfRangeException` for a
  negative `FlushTailChunks`, a negative endpoint rule (from the detector), or an invalid dither
  amplitude, all before taking any collaborator into use; then takes ownership of the encoder and
  decoder (disposed with this engine). `options` defaults to the documented values. Called only
  from `OnnxNemotronMultilingualRecognitionModel.CreateBackend`.
- **SampleRate**: the model's required input rate, 16000 Hz.
- **AcceptSamples(monoSamples)**: buffering only, no decoding. Processes the input one limiter
  frame (20 ms) at a time, so each slice is wholly passed or wholly removed and removed quiet is
  recorded exactly between the audio before and after it. Passed audio is dithered and handed to
  the extractor (closing any open padding range); removed samples are recorded as a mark at the
  current feature position, merged with a mark already at that position. Once loud audio has been
  seen and the limiter's quiet run reaches its 400 ms cap, the partly filled chunk is completed
  with silence features (`PadPartialChunk`), because no further audio will arrive to fill it and
  the last words would otherwise wait in it.
- **TryDecode(out result)**: when no result is pending, pumps the pipeline - decodes every
  complete 56-frame chunk, credits the removed-silence marks up to the decoded position (so a
  silent stream with no new frames still endpoints on the input clock), and queues a provisional
  result when the detokenized utterance text has changed - and dequeues one result. Returns
  `false` when nothing is pending.
- **TryFlush(out result)**: pumps; feeds the limiter's partial frame; if real audio arrived since
  the last flush tail, completes the partial chunk and appends `FlushTailChunks` whole chunks of
  silence features (`AppendSilenceTail`, padding-marked) so the encoder's look-ahead releases the
  last words; decodes them; credits the remaining marks; reports the remaining tokens as final;
  and returns every final of the flushed audio joined with spaces (earlier provisional results are
  superseded). Returns `false` when no text was produced. The extractor's own `Flush()` is not
  used: a live stream continues with silence, not with a zero pad.
- **Reset()**: session reset; returns the limiter, dither, pending results, encoder, decoder,
  extractor, detector, pre-encode carry, tokens, marks, padding ranges, counters, and flags to the
  start-of-stream state.
- **Dispose()**: disposes the decoder and encoder once; later operations throw
  `ObjectDisposedException`.
- **DecodeChunk()** (private): copies the pre-encode carry into the encoder input, dequeues 56
  feature frames after it, carries the chunk's last nine frames forward, and encodes the 65-frame
  input. Each of the `n` output frames stands for `56 * 160 / n` input samples (1280 samples, 80
  ms, for the real model's seven frames). For output frame `k`, whose first feature position is
  `chunkStart + k * 56 / n`, it credits the marks at or before that position, decodes the frame
  alone (`Decode(frame, 1, hidden, tokens)`), drops leading closing punctuation when the
  utterance was empty before the frame, reports the frame to the detector
  (`AdvancePaddingFrame` when the position lies in a padding range, otherwise `AdvanceFrame`),
  and applies the detector's decision. After the last frame it credits the marks at or before the
  chunk end.
- **DropLeadingClosingPunctuation()** (private): removes the tokens at the start of a segment
  whose text is only sentence-closing punctuation (Latin `.,;:!?`, the ellipsis, the CJK and
  full-width forms, and the Unicode closing brackets and final quotes); the frame counts as speech
  only when other tokens remain. The removed tokens have already advanced the prediction network.
- **CheckEndpoint()** (private): asks the detector; with no tokens any rule only restarts the
  detector; rules 1 and 2 queue the provisional text if it changed, then the final, clear the
  tokens, and restart the detector; rule 3 calls `SplitAtLastWord()`.
- **SplitAtLastWord()** (private): finds the last token that starts a word
  (`NemotronVocabulary.StartsWord`); when one exists after the first token, reports the tokens
  before it as final and keeps the rest; in every case restarts the detector with
  `Reset(carriesSpeech: true)`, so a word is never cut in two or lost.

**Endpointing**: decided by `NemotronEndpointDetector` on the decode clock (see
`nemotron-endpoint-detector.md`): 1.2 s of trailing silence after speech, 2.4 s of trailing
silence in any case, or 20 s of utterance. Trailing silence is decoded blank frames plus the quiet
input the limiter removed, credited in stream order. At an endpoint the encoder caches, decoder
state, and feature stream are deliberately kept, so speech that resumes is decoded with its full
left context and no audio is held or replayed; silence alone never produces a result.

**Silence padding**: a chunk is only ever completed with dithered digital silence run through the
feature extractor, never with all-zero log-mel rows, which the model hears as loud input and
answers with invented words. Padding frames never advance the endpoint clocks.

**NemotronEncoder (collaborator)**: the engine drives the encoder only through the
`INemotronEncoder` seam (`HiddenSize`, `Encode`, `Reset`), which exists so this engine's
chunking, endpointing, and flush logic can be unit tested with a managed fake. The ONNX Runtime
implementation, `NemotronEncoder`, is documented in `nemotron-encoder.md`.

**Error Handling**: construction throws `ArgumentNullException` for a null required argument and
`ArgumentOutOfRangeException` for invalid options; every operational member throws
`ObjectDisposedException` after disposal; ONNX Runtime exceptions from the encoder or decoder
propagate to Speech's dedicated worker, which converts them into a faulted recognition session.

**Dependencies**: `NemotronFeatureExtractor`, `NemotronEncoder` (through `INemotronEncoder`),
`NemotronRnntGreedyDecoder`, `NemotronEndpointDetector`, `NemotronEngineOptions`,
`SilenceRunLimiter`, and `DitherNoise` from this subsystem; `NemotronVocabulary` (detokenization
and `StartsWord`) from the ModelManagementSubsystem; and the core library's `IRecognitionBackend`
and `SpeechRecognitionResult`.

**Callers**: `OnnxNemotronMultilingualRecognitionModel` constructs the engine from its
`CreateBackend` implementation (the internal options overload lets the non-shipped evaluation tool
construct it with non-default options); the core library's recognition engine owns it and passes
it to each recognition session it creates.
