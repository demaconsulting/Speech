### OnnxChatterboxTurboEnglishSynthesisModel

**Purpose**: The one concrete `ISynthesisModel` this package ships. It declares the Chatterbox Turbo
English model's identity, downloadable files, voice choice, capability profile and output format,
and builds the loaded backend from an installed directory. Its companion types are
`ChatterboxTurboModelOptions`, `ChatterboxTurboLanguageModelVariant`,
`ChatterboxTurboAuxiliaryModelVariant` and `ChatterboxTurboEndOfSentenceTagPolicy`.

**Data Model**: Immutable after construction: the options, the resolved list of
`ChatterboxTurboReferenceVoice` values (supplied directly or loaded from a directory of `.wav`
files), the selected variants, and a static table of SHA-256 checksums keyed by file. The model id
is `chatterbox-turbo-onnx-en` (`BaseModelId`) for the default fp16 language model with q4 auxiliary
graphs, and `chatterbox-turbo-onnx-en-lm-<variant>-aux-<variant>` for any other combination.

**Key Members**:

- **Metadata**: id, display name, English language, license, 24 kHz mono `PreferredAudioFormat`,
  and the capability profile with native-tag support
- **DownloadDescriptor**: four graphs, each an `.onnx` plus `.onnx_data` file, with pinned
  checksums and relative install paths under `onnx/` (eight files by default)
- **Parameters**: a `voice` choice listing the supplied voices (first is default)
- **ResolveSpeakerId(parameters)**: maps the selected voice id to its index, defaulting to 0
- **CreateBackend(installDirectory)**: requires at least one voice, throws `FileNotFoundException`
  for any missing model file, builds the networks, tokenizer and engine, and disposes created
  sessions if a later step fails

**Error Handling**: `InvalidOperationException` when no voice exists at backend creation;
`FileNotFoundException` for missing files; ONNX Runtime exceptions propagate after cleanup.

**Dependencies**: `OnnxChatterboxTurboNetworks`, `OnnxChatterboxTurboSynthesisEngine`,
`ByteLevelBpeTokenizer`, `ChatterboxTurboCapabilityProfile`, `ChatterboxTurboReferenceVoice`.

**Callers**: `SpeechModelCatalogChatterboxTurboExtensions.AddChatterboxTurboModels` and Speech's
model store and synthesizer factory.
