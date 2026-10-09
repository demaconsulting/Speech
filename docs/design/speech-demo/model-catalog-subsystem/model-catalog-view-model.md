### ModelCatalogViewModel

**Purpose**: Present the model catalog panel's rows, selection, refresh and download commands,
and the empty-catalog message, driven entirely by `IModelCatalogService`.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `EmptyCatalogMessage` | `const string` | The explanation shown for an empty catalog |
| `Models` | `ObservableCollection<ModelListItemViewModel>` | The displayed rows |
| `SelectedModel` | `ModelListItemViewModel?` | The chosen row |
| `HasModels` / `IsCatalogEmpty` | `bool` | Whether rows exist |
| `RefreshCommand` | generated command | Re-reads the catalog |
| `DownloadCommand` | generated async command | Downloads the given row |
| `MirrorUrl` / `MirrorUser` / `MirrorPassword` / `MirrorBearerToken` | `string?` | The typed mirror-settings fields |
| `MirrorStatusMessage` / `MirrorHasError` | `string?` / `bool` | The outcome of the most recent `ApplyMirrorCommand` |
| `HasMirrorStatusMessage` / `MirrorAppliedSuccessfully` | `bool` | Derived display flags for the status message |
| `ApplyMirrorCommand` | generated command | Applies the typed mirror settings |

**Key Methods**:

- **Refresh()**: Captures the selected row's model id, clears the rows, rebuilds one row per
  reported descriptor, and then restores the captured id if it is still reported, or the first
  row otherwise. Rows are rebuilt rather than merged because the library's catalog is the single
  source of truth for both membership and state; keeping a stale row alive would let the panel
  offer a model the library no longer reports.
- **DownloadAsync(row, cancellationToken)**: Ignores a null row and any row that cannot
  currently be downloaded, then:
  1. Clears the row's previous failure message and progress and marks it `Downloading`, so the
     button disables and the progress bar appears before the first byte arrives
  2. Awaits the seam, forwarding the library's progress into the row's progress fraction
  3. Maps the library's reported outcome: `Installed` marks the row `Downloaded` at full
     progress; any other outcome marks it `FailedOrCorrupt` with an explanation derived from the
     outcome
  4. Treats cancellation as `NotDownloaded` with a "Download canceled." note, because the
     library guarantees a canceled download installs nothing
  5. Treats an unexpected seam exception as `FailedOrCorrupt` carrying that exception's message,
     because a presentation layer must not crash the application on a seam fault
- **ApplyMirror()**: Builds a `DownloadMirror?` from the typed `MirrorUrl`/`MirrorUser`/
  `MirrorPassword`/`MirrorBearerToken` fields via the shared `MirrorOptionsFactory` helper (the
  same validation rules the CLI's `Context.CreateMirror()` and the demo's launch-time
  `AppLaunchOptions.CreateDownloaderOptions()` already apply: the URL must be a valid absolute
  URI, a lone user or password is rejected, and a blank URL means "no mirror"), then calls
  `IModelCatalogService.ApplyMirror(mirror)`. On success, records a confirmation message and
  calls `Refresh()` so the row list reflects whatever the newly reconfigured catalog reports. On
  an `ArgumentException` (invalid typed input) or `InvalidOperationException` (the service
  cannot be reconfigured), records the exception's message as an error instead of letting it
  escape - consistent with how every other seam failure in this panel is reported in place
  rather than thrown at the user.

**Error Handling**: Never lets a seam fault, checksum mismatch, transport failure, or
cancellation escape as an unhandled exception; every outcome resolves to a row state and,
where applicable, a human-readable failure message. When the catalog reports nothing,
`IsCatalogEmpty` is true and the panel shows `EmptyCatalogMessage`. `ApplyMirror()` similarly
never lets a validation failure or an unsupported-reconfiguration request escape; both resolve
to `MirrorHasError` plus an explanatory `MirrorStatusMessage`.

**Dependencies**: `IModelCatalogService`, `ModelListItemViewModel`, the shared
`MirrorOptionsFactory` helper, and the library's descriptor, state, progress, result, and
`DownloadMirror` value types. Never touches `SpeechModelCatalog`, the file system, the
network, or Avalonia directly, which is what allows the whole download lifecycle - progress,
success, checksum mismatch, transport failure, seam fault, and cancellation - to be verified
deterministically with no network access and no downloadable model in existence.

**Callers**: `ModelCatalogView` (the Avalonia view bound to this presentation state, including
the mirror-settings controls and the "Apply mirror" button). The composition root
(`App.axaml.cs`) supplies this ViewModel's constructor with any launch-time
`AppLaunchOptions`-derived mirror values, so the panel's fields start pre-populated when the
demo was started with `--mirror-*` arguments.
