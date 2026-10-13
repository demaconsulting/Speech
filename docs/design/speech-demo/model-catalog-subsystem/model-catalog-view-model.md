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
| `RefreshCommand` | generated command | Re-reads the catalog; refused while a transfer is in flight |
| `RefreshErrorMessage` / `HasRefreshErrorMessage` | `string?` / `bool` | Explains a failed catalog re-read |
| `DownloadCommand` | generated async command | Downloads the given row; a null parameter is ignored |
| `MirrorUrl` / `MirrorUser` / `MirrorPassword` / `MirrorBearerToken` | `string?` | The typed mirror-settings fields |
| `MirrorStatusMessage` / `MirrorHasError` | `string?` / `bool` | The outcome of the most recent `ApplyMirrorCommand` |
| `HasMirrorStatusMessage` / `MirrorAppliedSuccessfully` | `bool` | Derived display flags for the status message |
| `ApplyMirrorCommand` | generated command | Applies the typed mirror settings |
| `VoicePacks` / `HasVoicePacks` | `ObservableCollection<...>` / `bool` | The pack rows; whether to show the section |
| `UserVoicesDirectory` / `VoiceConsentNotice` | `string?` / `string` | The user folder and the consent note |
| `InstallVoicePackCommand` / `RemoveVoicePackCommand` | generated async commands | Install or remove the given pack |
| `InitialLoadTask` | `Task` | Background load of installed packs' emotion flags; never faults |
| `VoiceBusyMessage` | `const string` | The explanation shown when a pack action is refused during a transfer |

**Key Methods**:

- **RefreshAsync()**: Asynchronous, guarded. Does nothing while any model download or voice-pack
  install is in flight (`IsTransferInFlight()`), so the row that shows that transfer's progress and
  outcome is not discarded. Otherwise it delegates to `ReloadAsync()`. This is the only entry point
  bound to the user's Refresh button.
- **ReloadAsync()** (private): The unguarded reload. Calls `RefreshCore()` and then
  `LoadPackEmotionsAsync()`. It is used by the commands that have just finished, or deliberately
  completed, a transfer themselves - `ApplyMirrorAsync`, a successful `InstallVoicePackAsync` and a
  successful `RemoveVoicePackAsync` - and so must not be blocked by the busy guard.
- **RefreshCore()** (private, synchronous): Captures the selected row's model id, clears the rows,
  rebuilds one row per reported descriptor, and then restores the captured id if it is still
  reported, or the first row otherwise. It also rebuilds the pack rows with `HasEmotions` false.
  Rows are rebuilt rather than merged because the library's catalog is the single source of truth
  for both membership and state; keeping a stale row alive would let the panel offer a model the
  library no longer reports. The constructor calls it directly. The rebuild is atomic: the new
  rows are built in a temporary list and published only once the whole enumeration succeeded, so
  a failing enumeration leaves the previous rows untouched.
- **ReloadReportingAsync(context)** (private): Runs `ReloadAsync()` and converts a fault into a
  message of the form "{context}, but the catalog could not be re-read: {reason}" on the caller's
  surface (mirror status or pack row). `RefreshAsync()` instead records `RefreshErrorMessage`,
  and a successful re-read clears it.
- **LoadPackEmotionsAsync()** (private): Awaits the service's `ReferenceVoicePackHasEmotionsAsync`
  for each installed pack row and sets the row's flag, so the file read never blocks the UI thread.
  A failed read leaves the flag false; the method never faults. The constructor starts it and
  exposes the task as `InitialLoadTask`.
- **DownloadAsync(row, cancellationToken)**: Ignores a null row and any row that cannot
  currently be downloaded. Refuses with `VoiceBusyMessage` on the row while a pack is installing
  (a pack change rebuilds, and so disposes, the catalog). Otherwise:
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
- **ApplyMirrorAsync()**: Asynchronous. While any model row is downloading or any pack row is
  installing, refuses outright - recording an explained `MirrorHasError`/`MirrorStatusMessage` and
  returning without calling `IModelCatalogService.ApplyMirror` at all - because that service call
  disposes the catalog instance any in-flight transfer depends on, which would otherwise abort it.
  Otherwise, builds a `DownloadMirror?` from the typed `MirrorUrl`/`MirrorUser`/
  `MirrorPassword`/`MirrorBearerToken` fields via the shared `MirrorOptionsFactory` helper (the
  same validation rules the CLI's `Context.CreateMirror()` and the demo's launch-time
  `AppLaunchOptions.CreateDownloaderOptions()` already apply: the URL must be a valid absolute
  URI, a lone user or password is rejected, and a blank URL means "no mirror"), then calls
  `IModelCatalogService.ApplyMirror(mirror)`. Blank (empty or whitespace) user name, password and
  bearer token values are treated as absent (`null`), consistent with `MirrorOptionsFactory`, so
  an empty field is never sent as an empty credential; the URL is not normalized. Failure messages
  never repeat the typed credentials. On success, records a confirmation message and then
  awaits the unguarded `ReloadAsync()` (not the guarded `RefreshAsync()`, because the transfer check has
  already passed) so the row list reflects whatever the newly reconfigured catalog reports. Any
  exception thrown by the mirror construction or by the service's `ApplyMirror` - an
  `ArgumentException` for invalid typed input, an `InvalidOperationException` because the service
  cannot be reconfigured, or any unexpected fault - is recorded as an error carrying the
  exception's message instead of escaping. The re-read is contained separately: if it throws after
  a successful reconfiguration, the panel reports "Mirror applied, but the catalog could not be
  re-read" as an error, so the confirmation is never left standing next to a failed reload. This is
  consistent with how every other seam failure in this panel is reported in place rather than
  thrown at the user.
- **InstallVoicePackAsync(pack, cancellationToken)**: Ignores a null row or one that cannot be
  installed. Refuses (setting `VoiceBusyMessage` on the row, without calling the service) while
  any model row is downloading or any pack is installing. Otherwise it reads the row's
  `EffectiveIncludeEmotions` (before the state changes, because an "Add emotions" upgrade always
  includes them), clears the row's failure message and progress, marks it `Downloading` and awaits
  the service's install, forwarding progress into the row.
  - A successful install awaits `ReloadReportingAsync`, because the service rebuilt the catalog (so
    Chatterbox re-reads its voices) and the rows must be re-read; a failed re-read is shown on the
    row as "installed, but the catalog could not be re-read".
  - After **every** unsuccessful install or upgrade outcome (a non-installed result, a checksum
    mismatch, cancellation or an exception), the private `ApplyPackOutcomeAsync` step re-reads the
    pack's state from the service. If the service reports the pack `Downloaded` - for example a
    failed "Add emotions" upgrade that the library rolled back to the previous voices-only install
    - the row stays `Downloaded`, its `HasEmotions` is re-read, and so removal stays enabled and
    "Add emotions" stays offered. Otherwise the row shows the outcome's state (`FailedOrCorrupt`,
    or `NotDownloaded` for a cancellation). The failure or cancellation message is preserved in
    every case. If the service cannot report the state (the re-read throws), the row falls back
    to the outcome's state.
- **RemoveVoicePackAsync(pack, cancellationToken)**: Ignores a null row or one that cannot be removed
  and refuses with `VoiceBusyMessage` under the same busy rule as the install. It does not mark
  the row `Downloading` and does not use `ApplyPackOutcomeAsync`. It awaits the service's
  asynchronous uninstall (which waits for any in-flight install of that pack) and then awaits
  `ReloadReportingAsync` so the pack's voices disappear (a failed re-read is reported on the row).
  It shows "Removal canceled." on the row when
  canceled and otherwise catches any exception from the service and shows its message on the row,
  so a store error never crashes the panel and leaves the pack installed.

**Error Handling**: Except where noted below, never lets a seam fault, checksum mismatch, transport failure, or
cancellation escape as an unhandled exception; every outcome resolves to a row state and,
where applicable, a human-readable failure message. When the catalog reports nothing,
`IsCatalogEmpty` is true and the panel shows `EmptyCatalogMessage`. `ApplyMirrorAsync()` similarly
never lets a validation failure, an in-progress download or pack install, an unsupported
reconfiguration, an unexpected seam fault or a failed catalog re-read escape; each resolves to
`MirrorHasError` plus an explanatory `MirrorStatusMessage`. `RefreshAsync()` refused during a transfer
is silent by design: it changes nothing and reports nothing; a catalog re-read that throws is contained
and shown through `RefreshErrorMessage`.
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
