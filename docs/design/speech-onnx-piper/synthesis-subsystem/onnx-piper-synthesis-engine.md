### OnnxPiperSynthesisEngine

**Purpose**: The production `IAnnotatedSynthesisBackend` that runs a Piper (VITS) voice's ONNX graph directly through
ONNX Runtime over the phoneme ids of the `PiperPhonemePipeline`.

**Data Model**:

- Constructor: `OnnxPiperSynthesisEngine(session, config, pipeline)`; takes ownership of the `InferenceSession`
  (disposed with the engine); a null argument throws `ArgumentNullException`
- Graph input names (constants): `input`, `input_lengths`, `scales`, `sid`
- `SampleRate` from the configuration, `UnknownSymbolCount` and `UnknownSymbols` (the diagnostics of the skipped
  symbols; the symbols are kept in a concurrent dictionary and the count with interlocked operations)

**Key Methods**:

- **Generate(text, speed, speakerId)**: speaks the text as written (no annotations); a null text throws
  `ArgumentNullException`
- **GenerateAnnotated(text, annotations, speed, speakerId)**: the same with the annotations of the English text analysis
  (the `en.norm`, `en.spell`, `en.reading` and `en.pron` kinds; other kinds are ignored by the phonemizer)
- Both: throw `ObjectDisposedException` after disposal; take the utterances of the pipeline for the text; report the
  skipped symbols of each; run the graph once per utterance with `scales` = [noise scale, length scale / speed, noise
  width] (a speed that is not a positive finite number counts as 1) and the speaker clamped into 0 to the voice's
  speaker count minus 1; return the single waveform, or the waveforms concatenated in order, or empty audio at the
  voice's rate when there is no utterance (the graph is not run)
- **RunProbeInference(session, config, pipeline)** (internal static): one run of the graph with the utterance of a
  sentence-sized probe text, so that a shape-dependent operator failure of an execution provider surfaces as an
  `OnnxRuntimeException`; throws `InvalidOperationException` when the voice's table cannot encode the probe text
- **Dispose()**: marks the engine disposed, then disposes the session; a second call does nothing

**Inference Contract.** A single forward pass: `input` (`int64`, `[1, N]`, the framed ids), `input_lengths` (`int64`,
`[1]`, N), `scales` (`float`, `[3]`) and `sid` (`int64`, `[1]`); the output is the waveform, its first output tensor
flattened. The input names are the standard Piper export's, read from the loaded real graph; the tests use a stand-in
graph with the same four inputs whose output echoes what it was fed (the ids, the three scales, the speaker and N).

**Error Handling**: null arguments throw `ArgumentNullException`; use after disposal throws `ObjectDisposedException`;
an ONNX Runtime failure of a run propagates to the caller. The disposed flag is set before the session is released so
that a concurrent call sees the flag instead of racing the release.

**Dependencies**: `PiperPhonemePipeline`, `PiperVoiceConfig`, `OnnxExecutionProviderSelector` (the caller of the probe),
the ONNX Runtime managed API.

**Callers**: `OnnxPiperLibriTtsEnSynthesisModel.CreateBackend`, which returns it to the Speech library as a backend.
