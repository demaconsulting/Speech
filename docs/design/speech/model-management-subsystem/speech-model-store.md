### SpeechModelStore

**Purpose**: Own the on-disk layout, atomic install/replace, and uninstall of downloaded models
under a single per-user root directory, resolving the download-while-in-use residual risk (an
in-progress synthesis/playback cycle reading `current/` while a redownload of the same model is
attempted) via the atomic staging-then-rename swap described below.

**Data Model**: `RootPath` (the resolved storage root). For each model id: `current/` (stable
installed content), `install-manifest.json` (a sidecar record of `TotalSizeBytes` and
`InstalledAtUtc`, written only after a successful swap), and `.tmp/{operationId}/` (scratch
staging space, never read as installed content). A private nested `InstallManifest` record
(`TotalSizeBytes`, `InstalledAtUtc`) is the manifest's serialized shape.

**Key Methods**:

- **SpeechModelStore(SpeechModelStoreOptions?)**: Resolves `RootPath` under
  `Environment.SpecialFolder.LocalApplicationData` by default, or from
  `SpeechModelStoreOptions.RootPathOverride` verbatim when supplied. Never throws.
- **IsInstalled(modelId)**: Returns `true` only when `current/` exists and a valid manifest
  accompanies it; a missing or unreadable manifest is treated as "not installed", never as an
  error.
- **BeginStaging(modelId)** _(internal)_: Creates a fresh, empty `.tmp/{operationId}/` staging
  directory, never touching `current/`. Called only by `SpeechModelDownloader`.
- **CompleteInstall(modelId, operationId, stagingDirectory, totalSizeBytes)** _(internal)_:
  Atomically swaps a verified staging directory into place as the new `current/` via a directory
  rename (renaming any existing `current/` aside first), then writes the manifest. Best-effort
  deletes the superseded directory afterward. The staging directory this promotes may already
  have been modified in place by the model's own `ISpeechModel.InstallAsync` hook, invoked by
  `SpeechModelDownloader` before this call; `SpeechModelStore` itself remains entirely
  `ISpeechModel`-agnostic and unchanged in this regard - it promotes whatever is physically
  present in the staging directory, whether or not something unpacked it first.
- **AbandonStaging(stagingDirectory)** _(internal, static)_: Deletes a failed/canceled operation's
  staging directory without touching `current/`.
- **Uninstall(modelId)**: Removes `current/` and the manifest for an installed model; a no-op for
  a model that was never installed.
- After the swap, `CompleteInstall` best-effort removes the now-empty `.tmp` root (a non-recursive delete,
  so another operation's staging directory keeps it alive and a failure is ignored).
- **CleanUpLeftovers(modelId)**: Best-effort deletes leftover `current.replaced-*` and `.tmp/*`
  directories from a prior interrupted operation.

**Error Handling**: `IsInstalled` and `CleanUpLeftovers` never throw for ordinary storage-state
conditions (missing model, missing, malformed, or field-less `{}` manifest, leftover directory
that cannot yet be deleted) - these degrade to an honest state or a best-effort no-op. All
members that accept a model id throw `ArgumentException` for a null, empty, whitespace-only,
invalid-file-name, `.`, or `..` id. `Uninstall` throws `SpeechModelStoreException` when `current/`
or the manifest cannot be removed, most commonly because another process still holds an open file
handle into it; after both are removed it runs `CleanUpLeftovers`, so leftover `.tmp/*` and
`current.replaced-*` directories are also deleted best-effort. `CompleteInstall` propagates
I/O failures; if the second directory rename fails after an existing `current/` was renamed
aside, it renames the previous content back into place (best-effort) before rethrowing, so a
previously installed model remains installed.

**Root Path**: `RootPath` is documented as an absolute path; an override is used verbatim and is
not validated or normalized, so a host is responsible for supplying an absolute path.

**Dependencies**: `SpeechModelStoreOptions`, `SpeechModelStoreException`, `System.Text.Json`.

**Callers**: `SpeechModelDownloader` (staging/swap members); hosts (install-state query,
uninstall, cleanup).
