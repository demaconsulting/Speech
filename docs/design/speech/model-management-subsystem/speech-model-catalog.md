### SpeechModelCatalog

**Purpose**: Enumerate the library's known/compiled-in models alongside each one's current
install state, and orchestrate downloading a known model by id, composing Sub-phase 2a's
storage/download machinery with the model contract.

**Data Model**: `KnownModels` (static, compiled-in `IReadOnlyList<ISpeechModel>`; currently
contains all four of this library's real, production models -
`SherpaOnnxZipformerEnRecognitionModel`, `SherpaOnnxNemotronStreamingEnRecognitionModel`,
`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`, and `SherpaOnnxKokoroEnglishSynthesisModel` - at
least one shippable model of every role the library defines, with a second, multi-speaker
synthesis model). Per instance: the injected known-model list, a `SpeechModelStore`, a
`SpeechModelDownloader`, and two in-memory `ConcurrentDictionary<string, byte>` sets tracking
model ids currently downloading and model ids whose most recent attempt failed.

**Key Methods**:

- **SpeechModelCatalog(options?, diagnostics?)**: Public constructor using the compiled-in
  `KnownModels`, a real `SpeechModelStore`, and a real `HttpModelDownloadClient`. Never throws.
- **SpeechModelCatalog(knownModels, store, client?, diagnostics?)** _(internal)_: Test-only
  constructor injecting the known-model list, store, and download client for deterministic
  testing without a real network.
- **Enumerate()**: Builds one `SpeechModelDescriptor` per known model, resolving each one's state
  via `GetState`. Never throws.
- **GetState(modelId)**: Returns `Downloading` when a `DownloadAsync` call for `modelId` is
  currently in flight on this instance; otherwise `Downloaded` when
  `SpeechModelStore.IsInstalled` reports it installed; otherwise `FailedOrCorrupt` when this
  instance's most recent attempt for it did not install the model; otherwise `NotDownloaded`.
- **DownloadAsync(modelId, progress?, cancellationToken)**: Looks up `modelId` in the known-model
  list, marks it downloading, delegates to `SpeechModelDownloader.DownloadAsync` with the model's
  own declared `DownloadDescriptor`, and records a failed-attempt marker when the outcome is not
  `Installed`. Throws `ArgumentException` when `modelId` matches no known model.

**Error Handling**: `Enumerate()` and `GetState(modelId)` never throw for an honest state query
(only `ArgumentException` for an invalid `modelId` string, matching `SpeechModelStore`'s own
validation). `DownloadAsync` throws `ArgumentException` for an unknown `modelId` - an explicit,
user-invoked action, never composition or enumeration - and propagates `OperationCanceledException`
for a canceled download without marking the model failed (cancellation is a caller request, not
a corruption signal).

**Dependencies**: `SpeechModelStore`, `SpeechModelDownloader`, `IModelDownloadClient`,
`ISpeechModel`, `SpeechModelDescriptor`, `ISpeechDiagnostics`.

**Callers**: Hosts building a model-settings page (enumerate + download/delete actions per
architecture.md's demo-application scope).
