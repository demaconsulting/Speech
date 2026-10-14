### NemotronRnntNetwork

**Purpose**: Provide the ONNX Runtime implementation of the `IRnntNetwork` seam that
`NemotronRnntGreedyDecoder` drives: the LSTM prediction network and the joint network of the
Nemotron RNN-T decoder. It is one of the two types in the package that run ONNX Runtime
inference.

**Data Model**: Wraps the prediction network (`decoder.onnx`: inputs `targets` [1, 1], `h_in` and
`c_in` [2, 1, 640]; outputs `decoder_output` [1, 640, 1], `h_out`, `c_out`) and the joint network
(`joint.onnx`: inputs `encoder_output` [1, 1, 1024] and `decoder_output` [1, 1, 640]; output
`joint_output` logits over the vocabulary). Both sessions are tiny and called many times per
chunk, so they are created with `CreateSessionOptions()` - one intra-op thread and
`session.intra_op.allow_spinning=0` - and always run on the CPU; a multi-threaded or spinning
pool would make these microsecond-scale calls slower and steal CPU from the encoder. All tensors
are `OrtValue`s over arrays allocated once and reused, so the per-token path performs no managed
allocation beyond ONNX Runtime's own result collection. `BlankPenalty` is the constant `3.0`.

**Key Methods**:

- **NemotronRnntNetwork(decoder, joint, blankId, encoderHiddenSize)**: takes ownership of both
  sessions; throws `ArgumentNullException` for a null session.
- **Reset()**: zeroes the recurrent state and advances the prediction network with the blank
  token, as at stream start.
- **Advance(token)**: feeds the emitted token and keeps the new recurrent state.
- **PredictToken(frame)**: runs the joint network on the encoder frame and the current decoder
  output and returns the arg-max token after subtracting `BlankPenalty` from the blank score.
- **SelectToken(logits, blankId)** (internal static, pure): the penalized arg-max; the first
  index wins ties. It is unit tested without ONNX models.
- **Dispose()**: releases both sessions and the tensors.

**Blank penalty rationale**: the model is under-confident on quiet, isolated microphone words
and otherwise emits blank for them; with the penalty 20 of 21 real microphone recordings of
single words and short phrases are recognized (versus about 10 without it), at the cost of
occasional near-homophone substitutions. Values above about 4 cause repeated or spurious tokens.

**Error Handling**: the constructor throws `ArgumentNullException` for a null session; every
operational member throws `ObjectDisposedException` after disposal; ONNX Runtime exceptions
propagate unchanged to the decoder and the engine.

**Dependencies**: `Microsoft.ML.OnnxRuntime` (`InferenceSession`, `OrtValue`); the
`IRnntNetwork` seam it implements.

**Callers**: `NemotronRnntGreedyDecoder` (through `IRnntNetwork`) predicts and advances through
it and resets and disposes it with the decoder; `OnnxNemotronMultilingualRecognitionModel
.CreateBackend` constructs it.
