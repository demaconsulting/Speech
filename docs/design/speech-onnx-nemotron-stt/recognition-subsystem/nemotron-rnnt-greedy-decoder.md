### NemotronRnntGreedyDecoder

**Purpose**: Run RNN-T greedy search over encoder output frames to produce token ids, over an
`IRnntNetwork` seam that holds the prediction network's recurrent state. The seam lets the greedy
loop be unit tested with a scripted managed fake; `NemotronRnntNetwork` is the ONNX Runtime
implementation, documented in `nemotron-rnnt-network.md`.

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

**Error Handling**: `Decode` throws `ArgumentNullException` for a null token list; ONNX Runtime
exceptions from the network propagate unchanged to the engine.

**Dependencies**: `IRnntNetwork` only; the ONNX-backed `NemotronRnntNetwork` is a separate unit.

**Callers**: `OnnxNemotronRecognitionEngine` decodes every encoder output through it and resets
and disposes it with the engine; `OnnxNemotronMultilingualRecognitionModel.CreateBackend`
constructs it over a `NemotronRnntNetwork`.
