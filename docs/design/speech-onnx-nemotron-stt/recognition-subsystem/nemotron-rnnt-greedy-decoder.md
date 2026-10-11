### NemotronRnntGreedyDecoder

**Purpose**: Run RNN-T greedy search over encoder output frames to produce token ids, over an
`IRnntNetwork` seam that holds the prediction network's recurrent state. The seam lets the greedy
loop be unit tested with a scripted managed fake; `NemotronRnntNetwork` is the ONNX Runtime
implementation, documented below.

**Data Model**: Holds the `IRnntNetwork` it owns. `MaxSymbolsPerFrame` is the constant `10`.
The decoder keeps no state of its own: the recurrent state lives in the network and persists
across `Decode` calls, so decoding continues seamlessly across chunk boundaries.

**Key Methods**:

- **NemotronRnntGreedyDecoder(network)**: takes ownership of the network and resets it to the
  start-of-stream state; throws `ArgumentNullException` for a null network.
- **Decode(encoderOutput, frameCount, hiddenSize, tokens)**: for each encoder frame, repeatedly
  asks the network for the arg-max token; a blank (`BlankId`, 13087 for the shipped vocabulary)
  moves to the next frame without advancing the network; any other token is appended to
  `tokens` and advances the prediction state once; at most ten symbols are emitted per frame.
  Returns the number of tokens emitted by the call. Throws `ArgumentNullException` for a null
  token list.
- **Reset()**: resets the network's prediction state.
- **Dispose()**: disposes the network.

**NemotronRnntNetwork (internal collaborator)**: the ONNX Runtime implementation of
`IRnntNetwork`. It wraps the LSTM prediction network (`decoder.onnx`: inputs `targets` [1, 1],
`h_in` and `c_in` [2, 1, 640]; outputs `decoder_output` [1, 640, 1], `h_out`, `c_out`) and the
joint network (`joint.onnx`: inputs `encoder_output` [1, 1, 1024] and `decoder_output`
[1, 1, 640]; output `joint_output` logits over the vocabulary). Both sessions are tiny and called
many times per chunk, so they are created with `CreateSessionOptions()` - one intra-op thread and
`session.intra_op.allow_spinning=0` - and always run on the CPU; a multi-threaded or spinning
pool would make these microsecond-scale calls slower and steal CPU from the encoder. All tensors
are `OrtValue`s over arrays allocated once and reused, so the per-token path performs no managed
allocation beyond ONNX Runtime's own result collection. `Reset` zeroes the recurrent state and
advances the prediction network with the blank token, as at stream start; `Advance(token)` feeds
the emitted token and keeps the new state; `PredictToken(frame)` runs the joint network and
returns the arg-max token after subtracting `NemotronRnntNetwork.BlankPenalty` (3.0) from the
blank score; the penalized arg-max is the pure internal static method `SelectToken(logits,
blankId)` (first index wins ties), which is unit tested without ONNX models. The model is
under-confident on quiet, isolated microphone words and otherwise emits blank for them; with the
penalty 20 of 21 real microphone recordings of single words and short phrases are recognized
(versus about 10 without it), at the cost of occasional
near-homophone substitutions. Values above about 4 cause repeated or spurious tokens.

**Error Handling**: `Decode` throws `ArgumentNullException` for a null token list; ONNX Runtime
exceptions from the network propagate unchanged to the engine.

**Dependencies**: `IRnntNetwork`; `NemotronRnntNetwork` additionally uses `Microsoft.ML.OnnxRuntime`.

**Callers**: `OnnxNemotronRecognitionEngine` decodes every encoder output through it and resets
and disposes it with the engine; `OnnxNemotronMultilingualRecognitionModel.CreateBackend`
constructs it over a `NemotronRnntNetwork`.
