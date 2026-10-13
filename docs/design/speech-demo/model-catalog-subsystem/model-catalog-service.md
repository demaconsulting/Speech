### ModelCatalogService

**Purpose**: Provide a demo-owned seam over the library's `SpeechModelCatalog` so the model
catalog panel can be exercised against controlled catalog data, and raise a notification once a
download genuinely installs a model.

**Why a Demo-Owned Seam**: `SpeechModelCatalog` is a sealed, disposable concrete type, and its
model-injecting constructor is internal to the library. A demo-owned interface over it is
therefore the only way to exercise the panel against controlled catalog data without adding
public API to the library. `IModelCatalogService` and `ModelCatalogService` are documented as
one unit because the interface has no independently observable behavior of its own - every test
exercises it through `ModelCatalogService`, its sole implementation. `IModelCatalogService`
itself does **not** extend `IDisposable`: disposal is a lifetime concern of whichever
composition root constructs an implementation (here, `App.axaml.cs`, which holds a concrete
`ModelCatalogService` reference specifically so it can dispose it at shutdown), not something
every interface consumer - `ModelCatalogViewModel`, the panel view models that only subscribe to
`ModelInstalled`, and every test fake - needs to also implement.

**Data Model**:

| Member | Returns | Behavior |
| --- | --- | --- |
| `Enumerate()` | `IReadOnlyList<SpeechModelDescriptor>` | Reports the library's models; retries once (see note) |
| `DownloadAsync(modelId, progress, cancellationToken)` | `Task<SpeechModelDownloadResult>` | Throws if id unknown |
| `ModelInstalled` | `event EventHandler<ModelInstalledEventArgs>?` | Raised once a model installs |
| `ApplyMirror(mirror)` | `void` | Rebuilds the catalog against a new mirror, or `null` to revert to public URIs |
| `ReferenceVoicePacks` / `UserVoicesDirectory` | list / `string?` | Packs (empty without a library), user folder |
| `GetReferenceVoicePackState(packId)` | `SpeechModelState` | The pack's install state |
| `ReferenceVoicePackHasEmotionsAsync(packId, ct)` | `Task<bool>` | Whether the installed pack has emotion variants |
| `InstallReferenceVoicePackAsync(packId, includeEmotions, ...)` | `Task<...Result>` | Installs a pack; rebuilds |
| `UninstallReferenceVoicePackAsync(packId, ct)` | `Task` | Removes a pack and rebuilds; waits for an install |
| `ReferenceVoicesChanged` | `event EventHandler?` | Raised after a rebuild caused by a voice or mirror change |

`Enumerate()` retries once if the catalog was just replaced.

**Key Methods**:

- **Enumerate()**: Forwards unchanged to the current `SpeechModelCatalog.Enumerate()`. If that
  catalog was disposed by a concurrent `ApplyMirror` between the read and the call, it retries
  once against the replacement (see Error Handling).
- **DownloadAsync(modelId, progress?, cancellationToken)**: Forwards to the injected catalog's
  `DownloadAsync`. Its one piece of added behavior is raising `ModelInstalled` after a call whose
  result reports `SpeechModelDownloadOutcome.Installed`: it looks up the installed model's `Role`
  from the catalog's own `Enumerate()` and raises the event with `(modelId, role)`, so the STT
  and TTS panels (each already depending on this same seam) can refresh themselves automatically
  the moment a matching-role model finishes installing, without a manual click or app restart.
  The event is never raised for a failed, canceled, or checksum-mismatched download attempt.
- **ApplyMirror(mirror)**: Supported only when this service was constructed with the
  two-argument constructor
  (`ModelCatalogService(SpeechModelCatalog, Func<SpeechModelDownloaderOptions?, SpeechModelCatalog>)`)
  or the composition constructor; otherwise throws `InvalidOperationException`. When supported, builds
  `SpeechModelDownloaderOptions` from `mirror` (or `null` to revert to each model's own public
  URI), calls the captured factory to rebuild an equivalent catalog against those options,
  swaps this service's forwarding target to the new catalog, and disposes the catalog it
  replaced immediately. An in-flight `DownloadAsync` call still running against the replaced
  catalog is therefore aborted rather than left to run to completion - its underlying HTTP
  operations observe a disposed object and fail. Callers must never invoke `ApplyMirror` while
  one of their own downloads is in progress; `ModelCatalogViewModel.ApplyMirrorAsync` (see its own
  design doc) is this demo's sole production caller and enforces that by refusing to call
  through - reporting an explained panel error instead - while any row is downloading.
- **Reference voice packs**: Supported when the service was constructed with the composition
  constructor (`ModelCatalogService(ModelCatalogComposition, SpeechModelDownloaderOptions?,
  Func<SpeechModelDownloaderOptions?, ModelCatalogComposition>)`), where the composition pairs the
  catalog with the one shared `ReferenceVoiceLibrary`. Install (whatever the outcome, because an upgrade
  may have moved the old install aside or restored it), uninstall and `ApplyMirror` call a private
  recompose step that builds a new pair through the factory, publishes it, disposes the old catalog and
  library, and raises `ReferenceVoicesChanged`. When an install throws or is canceled, the rebuild still
  runs but a failure of the rebuild itself is discarded so the install's own exception is the one that
  propagates; after a successful install a rebuild failure propagates. A failing factory leaves the
  previous pair published and undisposed, because the new pair is built before anything is replaced.
  `UninstallReferenceVoicePackAsync` first awaits the
  library's asynchronous uninstall, which serializes with an in-flight install of that pack
  (waiting for it, honoring cancellation); if the uninstall throws or is canceled the exception
  propagates unchanged and the recompose step and notification are skipped, because nothing changed. Services built
  with the
  single-catalog or catalog-factory constructors have no library:
  `ReferenceVoicePacks` is empty and `UserVoicesDirectory` is `null`, and `GetReferenceVoicePackState`,
  `ReferenceVoicePackHasEmotionsAsync`, install and uninstall all reject the call through the private `RequireVoiceLibrary`
  helper by throwing `InvalidOperationException` (the state is not reported as `NotDownloaded`).

**Error Handling**: `Enumerate()` never invents placeholder models when the library's registry is
empty - presenting an honest empty catalog is a presentation concern, not a reason to fabricate
data. `Enumerate()` does not otherwise handle failures, but when the catalog it read was disposed
by a concurrent `ApplyMirror` (an `ObjectDisposedException`), it retries exactly once against the
already-published replacement catalog; a second `ObjectDisposedException` propagates.
`DownloadAsync` propagates the library's own `ArgumentException` for an unknown model id.
`ApplyMirror` propagates `ArgumentException` from an invalid mirror configuration (surfaced by the
shared `MirrorOptionsFactory` helper its caller uses to build the `DownloadMirror`) and throws
`InvalidOperationException` when this service cannot be reconfigured. The reference-voice members
(state, emotion query, install, uninstall) throw `InvalidOperationException` when the service was
constructed without a voice library, and `UninstallReferenceVoicePackAsync` waits
for an in-flight install of that pack to finish (honoring cancellation) and then removes it.

**Thread Safety**: `ApplyMirror`, `Dispose` and the pack install and uninstall rebuilds all read and
then replace the same current catalog, voice library and downloader-options state with no
synchronization (the options field is not volatile), so concurrent calls could race on which pair
ends up current or disposed, and a rebuild disposes the previous pair, which aborts any
`DownloadAsync` still running against it. This panel invokes these members serially from the UI
thread, one command at a time, and its view model refuses overlapping transfers; any other caller
must provide its own external synchronization. `Enumerate()` and `DownloadAsync` carry no such
restriction beyond what the underlying `SpeechModelCatalog` itself documents.
**Ownership**: A service built with a factory or composition factory owns every catalog and voice
library it holds, disposing each replaced pair and the current pair on `Dispose`; otherwise it owns
neither.

**Dependencies**: The library's `SpeechModelCatalog`, `SpeechModelDescriptor`,
`SpeechModelDownloadResult`, `SpeechModelDownloadOutcome`, `SpeechModelDownloaderOptions`,
`DownloadMirror`, `ReferenceVoiceLibrary`, `ReferenceVoicePack` and `SpeechModelStoreException`,
and the subsystem's `ModelCatalogComposition` (the catalog and library pair its composition
constructor and factory exchange). A service constructed with the single-catalog constructor deliberately does
not dispose the catalog - the composition root owns that lifetime - so a shared catalog can
outlive any one adapter. A service constructed with a catalog factory instead owns every
catalog it ever points at (the one it started with and each one `ApplyMirror` later builds),
disposing whichever one is current when the service itself is disposed (`IDisposable`).

**Callers**: `ModelCatalogViewModel` (the catalog panel's presentation state, including its
mirror-settings controls); `SynthesisPanelViewModel` and the recognition panel view model (both
subscribe to `ModelInstalled` for auto-refresh-on-install). The composition root
(`App.axaml.cs`) constructs this service with a catalog factory so the mirror panel can
reconfigure it at runtime, and disposes it (rather than a separately tracked catalog reference)
at shutdown.
