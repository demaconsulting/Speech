### PiperVoiceConfig

**Purpose**: Read the settings of a Piper voice from the voice's `.onnx.json` file: the symbol table that turns phoneme
symbols into the integer ids the graph takes, the output sample rate, the speaker count and the voice's recommended
inference scales.

**Data Model**:

- `SampleRate` (`audio.sample_rate`, required and positive), `NumSpeakers` (`num_speakers`, 1 when absent),
  `NoiseScale`, `LengthScale` and `NoiseW` (`inference.noise_scale`, `inference.length_scale`, `inference.noise_w`, the
  defaults 0.667, 1.0 and 0.8 when absent), `EspeakVoice` (`espeak.voice`, empty when absent), and `SymbolCount`
- The symbol table (`phoneme_id_map`): single-symbol keys, compared ordinally and case-sensitively (the map holds
  symbols that differ only by case), each mapping to one or more ids
- The three framing symbols that every configuration must have: `^` (begin, `BeginIds`), `_` (pad, after each symbol,
  `PadIds`) and `$` (end, `EndIds`)
- The class is internal, immutable and thread-safe

**Key Methods**:

- **Parse(json)**: reads the text; throws `InvalidDataException` when it is not JSON, is not an object, has no
  `phoneme_id_map` object, has no positive `audio.sample_rate`, has a `phoneme_type` other than `espeak`, has an id that
  is not a number, or lacks `^`, `_` or `$` (the message names the missing symbol). The wrapped JSON errors keep the
  original exception as the inner exception
- **Load(path)**: reads the file as UTF-8 and parses it
- **TryGetIds(symbol, out ids)**: the ids of a symbol, or false

**Error Handling**: a null text is an `ArgumentNullException`, an empty path an `ArgumentException`; every other problem
is an `InvalidDataException`, never silent or garbled audio later.

**Dependencies**: `System.Text.Json` only.

**Callers**: `OnnxPiperLibriTtsEnSynthesisModel.CreateBackend`, `PiperPhonemePipeline` (the table and the framing) and
`OnnxPiperSynthesisEngine` (the rate and the scales).
