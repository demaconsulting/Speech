### ModelCatalogService

**Purpose**: Provide a demo-owned seam over the library's `SpeechModelCatalog` so the model
catalog panel can be exercised against controlled catalog data, and raise a notification once a
download genuinely installs a model.

**Why a Demo-Owned Seam**: `SpeechModelCatalog` is a sealed, disposable concrete type, and its
model-injecting constructor is internal to the library. A demo-owned interface over it is
therefore the only way to exercise the panel against controlled catalog data without adding
public API to the library. `IModelCatalogService` and `ModelCatalogService` are documented as
one unit because the interface has no independently observable behavior of its own - every test
exercises it through `ModelCatalogService`, its sole implementation.

**Data Model**:

| Member | Returns | Behavior |
| --- | --- | --- |
| `Enumerate()` | `IReadOnlyList<SpeechModelDescriptor>` | Never throws |
| `DownloadAsync(modelId, progress, cancellationToken)` | `Task<SpeechModelDownloadResult>` | Throws if id unknown |
| `ModelInstalled` | `event EventHandler<ModelInstalledEventArgs>?` | Raised once a model installs |
| `ApplyMirror(mirror)` | `void` | Rebuilds the catalog against a new mirror, or `null` to revert to each model's public URI |

**Key Methods**:

- **Enumerate()**: Forwards unchanged to the injected `SpeechModelCatalog.Enumerate()`.
- **DownloadAsync(modelId, progress?, cancellationToken)**: Forwards to the injected catalog's
  `DownloadAsync`. Its one piece of added behavior is raising `ModelInstalled` after a call whose
  result reports `SpeechModelDownloadOutcome.Installed`: it looks up the installed model's `Role`
  from the catalog's own `Enumerate()` and raises the event with `(modelId, role)`, so the STT
  and TTS panels (each already depending on this same seam) can refresh themselves automatically
  the moment a matching-role model finishes installing, without a manual click or app restart.
  The event is never raised for a failed, canceled, or checksum-mismatched download attempt.
- **ApplyMirror(mirror)**: Supported only when this service was constructed with the
  two-argument constructor (`ModelCatalogService(SpeechModelCatalog, Func<SpeechModelDownloaderOptions?, SpeechModelCatalog>)`);
  otherwise throws `InvalidOperationException`. When supported, builds
  `SpeechModelDownloaderOptions` from `mirror` (or `null` to revert to each model's own public
  URI), calls the captured factory to rebuild an equivalent catalog against those options,
  swaps this service's forwarding target to the new catalog, and disposes the catalog it
  replaced. An in-flight `DownloadAsync` call against the replaced catalog is left to run to
  completion rather than interrupted; only subsequent calls observe the new mirror.

**Error Handling**: `Enumerate()` never throws; it never invents placeholder models when the
library's registry is empty - presenting an honest empty catalog is a presentation concern, not
a reason to fabricate data. `DownloadAsync` propagates the library's own `ArgumentException` for
an unknown model id. `ApplyMirror` propagates `ArgumentException` from an invalid mirror
configuration (surfaced by the shared `MirrorOptionsFactory` helper its caller uses to build the
`DownloadMirror`) and throws `InvalidOperationException` when this service cannot be
reconfigured.

**Dependencies**: The library's `SpeechModelCatalog`, `SpeechModelDescriptor`,
`SpeechModelDownloadResult`, `SpeechModelDownloadOutcome`, `SpeechModelDownloaderOptions`,
`DownloadMirror`. A service constructed with the single-catalog constructor deliberately does
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
