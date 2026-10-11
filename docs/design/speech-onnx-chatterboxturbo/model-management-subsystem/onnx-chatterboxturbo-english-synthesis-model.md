### OnnxChatterboxTurboEnglishSynthesisModel

**Purpose**: The one concrete `ISynthesisModel` this package ships. It declares the Chatterbox Turbo
English model's identity, downloadable files, voice choice, capability profile and output format,
and builds the loaded backend from an installed directory. Its companion types are
`ChatterboxTurboModelOptions`, `ChatterboxTurboLanguageModelVariant`,
`ChatterboxTurboAuxiliaryModelVariant` and `ChatterboxTurboEndOfSentenceTagPolicy`.

**Data Model**: Immutable after construction: the options, the resolved list of
`ChatterboxTurboReferenceVoice` values (supplied directly, loaded from a directory of `.wav` or
`.flac` files, or lazily from the reference-voice library), the selected variants, and a static table of SHA-256
checksums keyed by file. The model id
is `chatterbox-turbo-onnx-en` (`BaseModelId`) for the default fp16 language model with q4 auxiliary
graphs, and `chatterbox-turbo-onnx-en-lm-<variant>-aux-<variant>` for any other combination.

**Key Members**:

- **Metadata**: id, display name, English language, license, 24 kHz mono `PreferredAudioFormat`,
  and the capability profile with native-tag support
- **DownloadDescriptor**: four graphs, each an `.onnx` plus `.onnx_data` file, with pinned
  checksums and relative install paths under `onnx/` (eight files by default)
- **Parameters**: a `voice` choice listing the available voices (first is default); its description
  appends the core `ReferenceVoiceLibrary.ConsentNotice`
- **Voice resolution** (constructor): voices are merged in this order, with the first id (ordinal) winning
  and later duplicates skipped with an `Info` diagnostic: explicit `ReferenceVoices` (eager), then
  `ReferenceVoiceDirectory` files (eager, sorted; a missing directory throws, and duplicates within these
  two sources still throw as before), then - unless `IncludeStoreVoices` is `false` - the voices of the
  core `ReferenceVoiceLibrary` (user folder, then installed pack), created lazily with
  `ChatterboxTurboReferenceVoice.FromFileLazy` so listing a hundred clips decodes none. A library voice
  whose display name clashes with an earlier one gets a `(user)` or `(pack)` suffix. The library is
  `ChatterboxTurboModelOptions.ReferenceVoiceLibrary` when set; the catalog extension
  `AddChatterboxTurboModels` otherwise builds `new ReferenceVoiceLibrary(catalog.Store)` (without
  mutating the caller's options). The list is fixed at construction, so it refreshes when the catalog is
  rebuilt after a pack is installed.
- **ResolveSpeakerId(parameters)**: maps the selected voice id to its index, defaulting to 0
- **CreateBackend(installDirectory)**: requires at least one voice, throws `FileNotFoundException`
  for any missing model file, builds the networks, tokenizer and engine, and disposes created
  sessions if a later step fails

**Error Handling**: `InvalidOperationException` when no voice exists at backend creation;
`FileNotFoundException` for missing files; ONNX Runtime exceptions propagate after cleanup.

**Dependencies**: `OnnxChatterboxTurboNetworks`, `OnnxChatterboxTurboSynthesisEngine`,
`ByteLevelBpeTokenizer`, `ChatterboxTurboCapabilityProfile`, `ChatterboxTurboReferenceVoice`,
core `ReferenceVoiceLibrary`.

**Callers**: `SpeechModelCatalogChatterboxTurboExtensions.AddChatterboxTurboModels` and Speech's
model store and synthesizer factory.
