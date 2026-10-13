### SpeechModelCatalogAdapter

**Purpose**: Provide the production implementation of `ICliModelCatalog`. The adapter owns, and disposes, one
real `SpeechModelCatalog` and one `ReferenceVoiceLibrary`, and forwards every seam member to them without
keeping any model state of its own.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `_catalog` | `SpeechModelCatalog` | The library catalog every model call is forwarded to |
| `_voiceLibrary` | `ReferenceVoiceLibrary` | The reference-voice library shared with the voice-cloning model |
| `_voicesDir` | `string?` | The extra `--voices-dir` folder, or `null` for only the user folder and installed packs |

**Key Methods**:

- **Constructor(options, downloaderOptions, voicesDir)**: Builds the catalog from the storage options and the
  downloader options (which carry the download mirror), and registers the model packages with
  `AddSherpaModels().AddKokoroModels().AddNemotronSttModels()`, because core `DemaConsulting.Speech` ships no
  built-in models. It then creates one `ReferenceVoiceLibrary` over the catalog's store and the same downloader
  options, and registers the voice-cloning model with `AddChatterboxTurboModels`, passing that library and
  `voicesDir` as the reference-voice directory. The model and the voice commands therefore see identical
  voices, the same store and the same mirror. If any of this fails part-way, the constructor disposes the
  voice library and the catalog it had already created and rethrows, so a failed composition leaks nothing.
- **Enumerate() / DownloadAsync(...)**: Forward to the catalog.
- **UninstallAsync(...) / CleanUpLeftoversAsync(...)**: Forward to the catalog's `Store.UninstallAsync` and
  `Store.CleanUpLeftoversAsync`.
- **GetPreferredAudioFormat(...) / CreateSynthesizerEngineAsync(...) / CreateRecognizerEngineAsync(...)**:
  Resolve the descriptor's model to the library's public `ISynthesisModel` or `IRecognitionModel` once, in a
  private helper, and refuse a model of the wrong role. The engine members then forward the descriptor's model,
  the catalog, the optional resolved parameter values and the cancellation token unchanged to
  `SpeechSynthesizerFactory.LoadAsync` or `SpeechRecognizerFactory.LoadAsync`. Each returns an engine with no
  device bound; the caller binds the playback or capture device afterwards.
- **VoicePacks / UserVoicesDirectory / EnumerateVoices() / GetVoicePackState(...)**: Forward to the voice
  library; `EnumerateVoices` adds the `--voices-dir` folder when one was given. Voices from that folder are
  decoded lazily, so an undecodable file does not fail composing the adapter and is still listed by name.
- **VoicePackHasEmotionsAsync(...) / InstallVoicePackAsync(...) / UninstallVoicePackAsync(...)**: Forward to
  the voice library.
- **Dispose()**: Disposes the voice library and the catalog.

**Error Handling**: A `null` descriptor is an `ArgumentNullException`. A model of the wrong role is an
`ArgumentException` whose message reads `Model '{id}' is not a synthesis model.` or
`Model '{id}' is not a recognition model.`, with parameter name `descriptor`. This branch is defensive in
production because `speak`, `recognize` and `ask` validate the role first and print their own operator-facing
message. All other errors (unknown id, store failure, cancellation) are produced by the library and pass
through unchanged. A construction failure propagates the original exception after cleanup.

**Dependencies**: The library's `SpeechModelCatalog`, `SpeechModelStore`, `SpeechModelStoreOptions`,
`SpeechModelDownloaderOptions`, `ReferenceVoiceLibrary`, `SpeechSynthesizerFactory`, `SpeechRecognizerFactory`
and the `AddSherpaModels`, `AddKokoroModels`, `AddNemotronSttModels` and `AddChatterboxTurboModels`
registration extensions.

**Callers**: `CliModelCatalogFactory`, which creates one adapter per command invocation, and the unit tests,
which create one over an isolated temporary store root.
