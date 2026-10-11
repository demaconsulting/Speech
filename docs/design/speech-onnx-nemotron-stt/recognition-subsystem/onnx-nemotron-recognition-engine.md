### OnnxNemotronRecognitionEngine

**Purpose**: Implement the core library's `IRecognitionBackend` seam against the Nemotron
streaming ONNX model, run directly through ONNX Runtime with no bundled native inference engine.
It owns the whole streaming pipeline - leading-silence limiting, dither, feature extraction,
chunked cache-aware encoding, greedy decoding, detokenization, and endpointing - and reports
provisional and final text. This package's `NemotronEncoder` and `NemotronRnntNetwork` are the
only types that run ONNX Runtime inference; both are documented here and in the
`NemotronRnntGreedyDecoder` unit rather than as separate units.

**Data Model**: Holds an `INemotronEncoder`, a `NemotronRnntGreedyDecoder`, a
`NemotronVocabulary`, a `NemotronEngineOptions`, a `SilenceRunLimiter`, a `DitherNoise`, a
`NemotronFeatureExtractor`, a reusable limiter-output list, a 65-frame encoder input array
(`EncoderFrames = PreEncodeFrames + ChunkFrames`, 128 floats per frame), the 9-frame pre-encode
carry array, the token ids of the utterance in progress, a queue of pending
`SpeechRecognitionResult`s, the last provisional text, and bookkeeping flags (`_dirty`: speech or
a token seen since the last model reset; `_audioSinceReset`; `_endpointRequested`;
`_emptyChunks`; `_chunksDecoded`; `_disposed`). `NemotronEngineOptions` holds the constants:
`ChunkFrames` 56, `PreEncodeFrames` 9, `DitherAmplitude` 1e-5, `EndpointQuietMs` 2500,
`EndpointEmptyChunks` 3, `FlushTailChunks` 1, plus the limiter options and the dither seed.

**Key Methods**:

- **OnnxNemotronRecognitionEngine(encoder, decoder, vocabulary, options)**: takes ownership of
  the encoder and decoder (disposed with this engine); throws `ArgumentNullException` for a null
  required argument; `options` defaults to the documented constants. Called only from
  `OnnxNemotronMultilingualRecognitionModel.CreateBackend`.
- **SampleRate**: the model's required input rate, 16000 Hz.
- **AcceptSamples(monoSamples)**: ignores empty input; otherwise passes the samples through the
  limiter, dithers the limiter output, and hands it to the feature extractor - buffering only, no
  decoding. It marks the engine dirty when the limiter has seen new loud samples and requests an
  input-clock endpoint when the engine is dirty and the limiter's `QuietRunSamples` has reached
  `EndpointQuietMs` (2500 ms). Otherwise, once the quiet run reaches the limiter's 400 ms cap
  (after which no more audio reaches the extractor), a partly filled chunk is completed with
  dithered silence so the last words are decoded promptly instead of waiting for the endpoint.
- **TryDecode(out result)**: when no result is pending, pumps the pipeline - decodes every
  complete 56-frame chunk, then queues a provisional result when the detokenized utterance text
  has changed, then finishes the utterance when an endpoint was requested - and dequeues one
  result. Returns `false` when nothing is pending.
- **TryFlush(out result)**: flushes the limiter's partial frame into the extractor, pumps, then
  finishes the utterance (extractor right pad, zero-padded final partial chunk, and
  `FlushTailChunks` all-zero tail chunk) and returns the final result; returns `false` when no
  audio or no text was produced. Earlier provisional results are superseded by the final one.
- **Reset()**: resets the limiter, dither, and pending results, and returns the encoder,
  decoder, extractor, and utterance bookkeeping to the start-of-stream state.
- **Dispose()**: disposes the decoder and encoder once; later operations throw
  `ObjectDisposedException`.
- **DecodeChunk(realFrames)** (private): copies the pre-encode carry into the encoder input,
  dequeues `realFrames` feature frames after it, zero-pads the chunk, carries the chunk's last
  nine frames forward, encodes the 65-frame input, and decodes the output; tracks consecutive
  token-free chunks once text exists.

**Endpointing**: an utterance is finalized in one of two ways. (a) *Input-clock quiet*: when the
input has been quiet for at least 2500 ms after speech, measured on the **input** clock through
the limiter's quiet-run length (because the limiter removes most of a long quiet run, so the
chunk clock stalls during silence), the buffered audio is flushed and decoded, the final result
is reported, and the full model state is reset for a clean next utterance while the limiter state
is kept. A stream of only silence never endpoints, because nothing has made the engine dirty.
(b) *Empty chunks*: three consecutive token-free chunks after text exists report a final result
but keep the model state so decoding continues seamlessly; this is the fallback for steady noise
that never reads as quiet.

**NemotronEncoder (internal collaborator)**: the ONNX Runtime implementation of the
`INemotronEncoder` seam (`HiddenSize` 1024, `Encode`, `Reset`), which exists so this engine's
chunking, endpointing, and flush logic can be unit tested with a managed fake. `Encode` feeds six
inputs - `audio_signal` [1, 65, 128], `length`, `cache_last_channel` [1, 24, 56, 1024],
`cache_last_time` [1, 24, 1024, 8], `cache_last_channel_len`, and `lang_id` - and reads five
outputs: the encoder frames, their length, and the three `*_next` caches. The caches stay in
native `OrtValue`s and are fed back as the next call's inputs without copying; the previous
call's outputs are released only after the next call has consumed them. `Reset` returns to
zero-initialized caches. The static `RunProbeInference(session)` runs one representative
inference so `OnnxExecutionProviderSelector.Create` can reject an accelerated candidate that
loads but cannot run the graph.

**Error Handling**: construction throws `ArgumentNullException` for a null required argument;
every operational member throws `ObjectDisposedException` after disposal; ONNX Runtime exceptions
from the encoder or decoder propagate to Speech's dedicated worker, which converts them into a
faulted recognition session.

**Dependencies**: `Microsoft.ML.OnnxRuntime` (`InferenceSession`, `OrtValue`, through
`NemotronEncoder`); `NemotronFeatureExtractor`, `NemotronRnntGreedyDecoder`, `SilenceRunLimiter`,
and `DitherNoise` from this subsystem; `NemotronVocabulary` from the ModelManagementSubsystem;
and the core library's `IRecognitionBackend` and `SpeechRecognitionResult`.

**Callers**: `OnnxNemotronMultilingualRecognitionModel` constructs the engine from its
`CreateBackend` implementation; the core library's recognition engine owns it and passes it to
each recognition session it creates.
