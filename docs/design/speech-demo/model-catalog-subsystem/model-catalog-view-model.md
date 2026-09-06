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

**Error Handling**: Never lets a seam fault, checksum mismatch, transport failure, or
cancellation escape as an unhandled exception; every outcome resolves to a row state and,
where applicable, a human-readable failure message. When the catalog reports nothing,
`IsCatalogEmpty` is true and the panel shows `EmptyCatalogMessage`.

**Dependencies**: `IModelCatalogService`, `ModelListItemViewModel`, and the library's descriptor,
state, progress, and result value types. Never touches `SpeechModelCatalog`, the file system, the
network, or Avalonia directly, which is what allows the whole download lifecycle - progress,
success, checksum mismatch, transport failure, seam fault, and cancellation - to be verified
deterministically with no network access and no downloadable model in existence.

**Callers**: `ModelCatalogView` (the Avalonia view bound to this presentation state).
