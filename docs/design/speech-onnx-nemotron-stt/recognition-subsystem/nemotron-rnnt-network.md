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
allocation beyond ONNX Runtime's own result collection. `BlankPenalty` is the validated blank
penalty given at construction (default `DefaultBlankPenalty` = 1.0).

**Key Methods**:

- **NemotronRnntNetwork(decoder, joint, blankId, blankPenalty, encoderHiddenSize)**: validates the
  arguments first - `ArgumentNullException` for a null session, `ArgumentOutOfRangeException`
  for a negative or non-finite `blankPenalty` - and only then takes ownership of both sessions,
  so on an exception the caller still owns and disposes them. `blankPenalty` defaults to
  `DefaultBlankPenalty`.
- **ValidateBlankPenalty(blankPenalty)** (internal static): throws `ArgumentOutOfRangeException`
  unless the penalty is finite and not negative. A negative penalty would favor blank and
  suppress words, and a non-finite one would make every comparison meaningless, so both are
  configuration errors. The model calls it before reading any file.
- **Reset()**: zeroes the recurrent state and advances the prediction network with the blank
  token, as at stream start.
- **Advance(token)**: feeds the emitted token and keeps the new recurrent state.
- **PredictToken(frame)**: runs the joint network on the encoder frame and the current decoder
  output and returns the arg-max token after subtracting `BlankPenalty` from the blank score.
- **SelectToken(logits, blankId, blankPenalty)** (internal static, pure): the arg-max after
  subtracting `blankPenalty` from the blank logit (zero is plain greedy selection); the first
  index wins ties. It is unit tested without ONNX models.
- **Dispose()**: releases both sessions and the tensors.

**Blank penalty rationale**: with no penalty the model is under-confident on quiet, isolated
words and emits blank for them; a large penalty makes it emit words early and then repeat them
(for example "Sun sunset" after background noise), and values above about 4 cause repeated or
spurious tokens. The default was chosen by measurement: a sweep from 0 to 3 in steps of 0.5 with
the non-shipped evaluation tool over the committed evaluation data (recorded in
`docs/evaluation/nemotron-stt/README.md`) gave the lowest word error rate on the LibriSpeech
subset at 0.5 and 1.0 (3.39%, against 4.82% at 3.0). 1.0 is the default because it also
finalizes exactly one result per LibriSpeech utterance and gives the lowest word error rate on the
paused and continuous scenarios, while quiet speech differs from 0.5 by one word; no value
produced invented words. The previous fixed value, 3.0, had been tuned on a few microphone recordings of
single words and cost accuracy on ordinary speech. The penalty is configurable only through the
internal `NemotronEngineOptions.BlankPenalty`, so it can be swept without a public API.

**Error Handling**: the constructor throws `ArgumentNullException` for a null session and
`ArgumentOutOfRangeException` for an invalid blank penalty, before taking ownership; every
operational member throws `ObjectDisposedException` after disposal; ONNX Runtime exceptions
propagate unchanged to the decoder and the engine.

**Dependencies**: `Microsoft.ML.OnnxRuntime` (`InferenceSession`, `OrtValue`); the
`IRnntNetwork` seam it implements.

**Callers**: `NemotronRnntGreedyDecoder` (through `IRnntNetwork`) predicts and advances through
it and resets and disposes it with the decoder; `OnnxNemotronMultilingualRecognitionModel
.CreateBackend` validates the penalty with `ValidateBlankPenalty` and constructs it with
`NemotronEngineOptions.BlankPenalty`.
