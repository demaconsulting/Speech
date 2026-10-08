### SpeechModelCatalog

**Purpose**: Enumerate the models a host has registered with the catalog alongside each one's
current install state, and orchestrate downloading a known model by id, composing Sub-phase 2a's
storage/download machinery with the model contract.

**Data Model**: Per instance: a mutable known-model list (`List<ISpeechModel>`), which starts
empty for the public constructor - the library itself ships zero built-in models - and is
populated by the host through `AddModels(...)` or an extension method layered on it (for example
the sibling `SpeechSherpa` system's `AddSherpaModels()`, see _SpeechSherpa
ModelManagementSubsystem Design_); a `SpeechModelStore`, a
`SpeechModelDownloader`, and two in-memory `ConcurrentDictionary<string, byte>` sets tracking
model ids currently downloading and model ids whose most recent attempt failed. **Store**
(public, get-only) - the `SpeechModelStore` this catalog composes internally, exposed so a host
can compose a recognizer/synthesizer through the same catalog instance used for enumeration and
download, without constructing a second store.

**Key Methods**:

- **SpeechModelCatalog(options?, diagnostics?)**: Public constructor starting from an empty
  known-model list, a real `SpeechModelStore`, and a real `HttpModelDownloadClient`. Never throws.
- **SpeechModelCatalog(knownModels, store, client?, diagnostics?)** _(internal)_: Test-only
  constructor seeding the known-model list and injecting the store and download client for
  deterministic testing without a real network.
- **AddModels(params ISpeechModel[] models)**: Appends the given models to this catalog's
  known-model list and returns the same catalog instance, so registrations chain fluently (for
  example `new SpeechModelCatalog().AddSherpaModels()`). Throws `ArgumentNullException` for a null
  array. This is a builder-phase operation: a host must finish every `AddModels` call before
  sharing the catalog for concurrent `Enumerate`/`GetState`/`DownloadAsync` use, since the list
  itself is not synchronized against concurrent mutation.
- **Store**: Returns the `SpeechModelStore` instance this catalog was composed with. Never throws.
- **Enumerate()**: Builds one `SpeechModelDescriptor` per known model, resolving each one's state
  via `GetState`. Never throws.
- **GetState(modelId)**: Returns `Downloading` when a `DownloadAsync` call for `modelId` is
  currently in flight on this instance; otherwise `Downloaded` when
  `SpeechModelStore.IsInstalled` reports it installed; otherwise `FailedOrCorrupt` when this
  instance's most recent attempt for it did not install the model; otherwise `NotDownloaded`.
- **DownloadAsync(modelId, progress?, cancellationToken)**: Looks up `modelId` in the known-model
  list, marks it downloading, delegates to `SpeechModelDownloader.DownloadAsync` with the model's
  own declared `DownloadDescriptor`, and records a failed-attempt marker when the outcome is not
  `Installed`. Throws `ArgumentException` when `modelId` matches no known model. Because
  `SpeechModelDownloader.DownloadAsync` itself now short-circuits to a safe no-op for a model id
  it finds already installed (see `SpeechModelDownloader`'s design doc), calling this method for
  an already-`Downloaded` model id is likewise a safe no-op that returns `Installed` without any
  network activity - a host may call it unconditionally on every launch without first checking
  `GetState`.

**Error Handling**: `AddModels` throws `ArgumentNullException` for a null array, a programming
error. `Enumerate()` and `GetState(modelId)` never throw for an honest state query
(only `ArgumentException` for an invalid `modelId` string, matching `SpeechModelStore`'s own
validation). `DownloadAsync` throws `ArgumentException` for an unknown `modelId` - an explicit,
user-invoked action, never composition or enumeration - and propagates `OperationCanceledException`
for a canceled download without marking the model failed (cancellation is a caller request, not
a corruption signal).

**Dependencies**: `SpeechModelStore`, `SpeechModelDownloader`, `IModelDownloadClient`,
`ISpeechModel`, `SpeechModelDescriptor`, `ISpeechDiagnostics`.

**Callers**: Hosts building a model-settings page (enumerate + download/delete actions per
the demo-application scope). Model-supplying extension methods such as `SpeechSherpa`'s
`AddSherpaModels()` (via `AddModels`). `SpeechRecognizerFactory`/`SpeechSynthesizerFactory`
catalog-based `LoadAsync` overloads (via `Store`).
