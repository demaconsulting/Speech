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
| `VoicePacks` / `HasVoicePacks` | `ObservableCollection<...>` / `bool` | The pack rows; whether to show the section |
| `UserVoicesDirectory` / `VoiceConsentNotice` | `string?` / `string` | The user folder and the consent note |
| `InstallVoicePackAsync` / `RemoveVoicePack` | generated async commands | Install or remove the given pack |
| `InitialLoadTask` | `Task` | Background load of installed packs' emotion flags; never faults |
| `VoiceBusyMessage` | `const string` | The explanation shown when a pack action is refused during a transfer |

**Key Methods**:

- **Refresh()**: Asynchronous. Captures the selected row's model id, clears the rows, rebuilds one row per
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
- **Refresh()**: Rebuilds the rows from the catalog and re-reads the installed packs' emotion flags.
  It does nothing while any model download or voice-pack install is in flight, so the row that
  shows that transfer's progress and outcome is not discarded; the commands that have just finished
  a transfer reload the rows directly.
- **ApplyMirror()**: Asynchronous (it awaits `Refresh()`). While any row's `IsDownloading` is true, refuses outright -
  recording an
  explained `MirrorHasError`/`MirrorStatusMessage` and returning without calling
  `IModelCatalogService.ApplyMirror` at all - because that service call disposes the catalog
  instance any in-flight `DownloadAsync` call depends on, which would otherwise abort that
  download. Otherwise, builds a `DownloadMirror?` from the typed `MirrorUrl`/`MirrorUser`/
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

- **InstallVoicePackAsync(pack, cancellationToken) / RemoveVoicePack(pack, cancellationToken)**: Refuse (setting
  `VoiceBusyMessage` on the row, without calling the service) while any model row is downloading
  or any pack is installing. Otherwise they mark the row `Downloading` (install) and call the
  seam (an install passes the row's `EffectiveIncludeEmotions`, read before the state changes;
  `Refresh()` awaits the service's `ReferenceVoicePackHasEmotionsAsync` for each installed row, so the file
    read never blocks the UI thread; the constructor builds the rows synchronously with `HasEmotions` false
    and sets `InitialLoadTask`, which fills the flags in afterwards).
  - A successful install or removal calls `Refresh()`.
  - After **every** unsuccessful install or upgrade outcome (a non-installed result, a checksum
    mismatch, cancellation or an exception), a private `ApplyPackOutcomeAsync` step re-reads the pack's
    state from the service. If the service reports the pack `Downloaded` - for example a failed
    "Add emotions" upgrade that the library rolled back to the previous voices-only install - the
    row stays `Downloaded`, its `HasEmotions` is re-read, and so removal stays enabled and "Add
    emotions" stays offered. Otherwise the row shows the outcome's state (`FailedOrCorrupt`, or
    `NotDownloaded` for a cancellation). The failure or cancellation message is preserved in
    every case. If the service cannot report the state (the re-read throws), the row falls back
    to the outcome's state.
  - `RemoveVoicePack` awaits the service's asynchronous uninstall (which waits for any in-flight install
    of that pack). It shows "Removal canceled." on the row when canceled and otherwise catches any
    exception from the service and shows its message on the row, so a store error never crashes the
    panel and leaves the pack installed.
  - `DownloadAsync` and `ApplyMirror` in turn refuse while a pack is installing, because a pack
    change rebuilds (and disposes) the catalog. `Refresh()` also rebuilds the pack rows.

**Error Handling**: Never lets a seam fault, checksum mismatch, transport failure, or
cancellation escape as an unhandled exception; every outcome resolves to a row state and,
where applicable, a human-readable failure message. When the catalog reports nothing,
`IsCatalogEmpty` is true and the panel shows `EmptyCatalogMessage`. `ApplyMirror()` similarly
never lets a validation failure, an in-progress download, or an unsupported-reconfiguration
request escape; each resolves to `MirrorHasError` plus an explanatory `MirrorStatusMessage`.
Voice-pack operations are fault-contained the same way: an install outcome or exception is
reported on the pack's row (see `ApplyPackOutcomeAsync` above), a removal fault is shown as the row's
failure message, and an operation refused because a transfer is in flight shows `VoiceBusyMessage`
on the row. The mirror password and bearer token are held as plain strings in this demo (the view
masks them with a password character); a production host should keep them in a secret store.

**Dependencies**: `IModelCatalogService`, `ModelListItemViewModel`,
`ReferenceVoicePackListItemViewModel`, the shared `MirrorOptionsFactory` helper, and the library's
descriptor, state, progress, result, and `DownloadMirror` value types, plus the library's
`ReferenceVoicePack` value type and the static `ReferenceVoiceLibrary.ConsentNotice` constant.
It never touches a `SpeechModelCatalog` or `ReferenceVoiceLibrary` instance, the file system, the
network, or Avalonia directly, which is what allows the whole download lifecycle - progress,
success, checksum mismatch, transport failure, seam fault, and cancellation - to be verified
deterministically with no network access and no downloadable model in existence.

**Callers**: `ModelCatalogView` (the Avalonia view bound to this presentation state, including
the mirror-settings controls and the "Apply mirror" button). The composition root
(`App.axaml.cs`) supplies this ViewModel's constructor with any launch-time
`AppLaunchOptions`-derived mirror values, so the panel's fields start pre-populated when the
demo was started with `--mirror-*` arguments.
