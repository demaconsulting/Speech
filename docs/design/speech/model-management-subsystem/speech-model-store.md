### SpeechModelStore

**Purpose**: Own the on-disk layout, atomic install/replace, and uninstall of downloaded models
under a single per-user root directory, resolving architecture.md's Open Concern #3
(download-while-in-use).

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
- **CleanUpLeftovers(modelId)**: Best-effort deletes leftover `current.replaced-*` and `.tmp/*`
  directories from a prior interrupted operation.

**Error Handling**: Every member except `Uninstall` never throws for ordinary storage-state
conditions (missing model, missing/corrupt manifest, leftover directory that cannot yet be
deleted) - these degrade to an honest state or a best-effort no-op. `Uninstall` throws
`SpeechModelStoreException` only when `current/` cannot be removed, most commonly because
another process still holds an open file handle into it.

**Dependencies**: `SpeechModelStoreOptions`, `SpeechModelStoreException`, `System.Text.Json`.

**Callers**: `SpeechModelDownloader` (staging/swap members); hosts (install-state query,
uninstall, cleanup).
