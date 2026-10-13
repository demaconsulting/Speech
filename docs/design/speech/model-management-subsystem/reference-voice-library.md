### ReferenceVoiceLibrary

**Purpose**: One model-agnostic place for reference voices: install and remove downloadable packs,
keep a folder for the user's own clips, and list every available voice merged into one ordered,
de-duplicated list. Any voice-cloning model can consume it; `ChatterboxTurbo` is the first.

**Data Model**: A public sealed `IDisposable` constructed from a `SpeechModelStore` (typically
`catalog.Store`), optional `SpeechModelDownloaderOptions` (mirror) and optional `ISpeechDiagnostics`.
It owns a second `SpeechModelStore` rooted at `{store root}\{RootDirectoryName}` (the public constant
`RootDirectoryName`, `reference-voices`; `UserDirectoryName` is `user`) and creates its
`SpeechModelDownloader` lazily. In-memory sets track pack ids installing or failed since process start.

Disk layout (the unit's contract with the CLI, demo and documentation; the requirements state only that packs
and the user folder live in a dedicated area inside the store root, so this hierarchy, the `current` folder
and the mood-to-path representation of `MoodClips` are design decisions recorded here):

```text
{models root}\reference-voices\
    user\                                  <- the user's own clips (see EnumerateVoices; never touched by packs)
    voice-zero-1.0\
        install-manifest.json              <- written by SpeechModelStore
        current\
            pack.json  LICENSE.md  README.md
            voices\<id>.flac ...  voices\README.md
            voices-emotion\<id>\<mood>.flac ...  voices-emotion\README.md   <- only when installed with emotions
```

**Key Methods**: `RootPath`, `UserDirectory`, static `Packs`, `EnsureUserDirectory()`,
`GetPackDirectory(packId)`, `GetPackState(packId)`, `InstallPackAsync(packId, includeEmotions = true, progress, ct)`
(plus a source-compatible
`InstallPackAsync(packId, progress, ct)` overload that includes emotions), `UninstallPackAsync(packId, ct)`,
`PackHasEmotionsAsync(packId, ct)`, `GetPackInstalledVariantsAsync(packId, ct)`, `EnumerateVoices(extraDirectories)`, `Dispose()`.

- `InstallPackAsync` calls the downloader's internal install-hook overload with the pack's descriptor
  and `ReferenceVoicePackInstaller.InstallAsync` as the hook, so the whole archive is fetched through
  the hash-verified, mirror-aware path (with a mirror the URL is
  `{mirror}/voice-zero-1.0/voice-zero-1.0.zip`), only a verified archive is extracted, and the result is
  replaced through the store's rollback-protected install. A second call for an installed pack is a no-op returning `Installed`.
- `includeEmotions` (default `true`) also extracts the emotion clips (see `ReferenceVoicePackInstaller`).
  **Upgrade**: when emotions are requested and the installed pack lacks them, the library renames
  `current` to a sibling `current.upgrade-backup-{guid}` folder (the downloader treats an installed id
  as complete and would otherwise skip the download) and installs again. A `finally` deletes the backup
  on an `Installed` outcome and renames it back on any other outcome or exception, so a failed or
  canceled re-download keeps the previous install; a leftover backup from a crash is restored (or
  deleted when `current` exists) at the start of the next install. A rename that fails because files are
  in use throws `SpeechModelStoreException`. **Concurrency**: whole `InstallPackAsync` calls for one pack
  are serialized by a per-pack `SemaphoreSlim` (taken before the upgrade check), and the in-progress
  state is reference counted so `GetPackState` reports `Downloading` until the last concurrent caller
  finishes. An install that already has emotions is never downgraded by `includeEmotions: false`,
  and a voices-only request over a voices-only install is the usual no-op.
- `GetPackInstalledVariantsAsync` reads `pack.json` (with `File.ReadAllTextAsync`) and returns its `variants` (empty
  for a missing, unreadable or malformed file; it never throws); `PackHasEmotionsAsync` is true when
  `voices-emotion` is among them. A missing or corrupt `pack.json` therefore reads as "no emotions", so a later
  install with emotions upgrades (downloads) the pack again.
- `GetPackState` reports `Downloading`, `Downloaded`, `FailedOrCorrupt` (a failed attempt in this
  process) or `NotDownloaded`, and never throws for a known id.
- `UninstallPackAsync` removes the pack only; the user folder is never touched. It awaits (honoring its
  `CancellationToken`) the same per-pack lock as `InstallPackAsync`, so an uninstall that arrives while an
  install (or another uninstall) of that pack is in flight waits for it to finish and then runs; the two never
  interleave and nothing is thrown for the overlap. Canceling while waiting throws
  `OperationCanceledException` and leaves the install and the pack untouched; once the removal starts it runs to
  completion. The directory deletions run on the thread pool through `SpeechModelStore.UninstallAsync`
  (there is no asynchronous directory-delete API). After the store uninstall it also deletes every
  `current.upgrade-backup-*` folder left in the pack
  directory so a leftover backup cannot resurrect the pack, and clears the pack's failed flag.
- **Asynchronous policy**: the pack operations are asynchronous; `EnumerateVoices`,
  `EnsureUserDirectory` and `GetPackState` stay synchronous: they are directory-metadata scans or in-memory
  reads, and `EnumerateVoices` is needed synchronously by public engine-constructor wiring. The upgrade
  backup move, restore and delete steps run on the thread pool so a large pack never blocks the caller.
- `EnumerateVoices` returns, in order, voices from the caller-named directories (in the order given),
  then the user folder, then each installed pack. Within a directory files are sorted by name
  (ordinal); the extensions in `AudioFileReader.SupportedExtensions` (`.wav` and `.flac`, any case) are
  accepted. The first voice with a given id (ordinal)
  wins and later duplicates are skipped with an `Info` diagnostic. The id is the file name without
  extension; the display name is a humanized form (`alan_davis_drake2` becomes `Alan Davis Drake 2`).
  Pack voices carry the pack's license and attribution. For a pack installed with emotions, each pack
  voice's `MoodClips` maps the lower-case mood (file stem) to the absolute path of
  `voices-emotion\{voiceId}\{mood}.{ext}` (the shipped pack uses `.flac`; any supported extension is
  found), ordinally sorted, or is `null` when there are none; a clip whose stem differs only in case or
  surrounding whitespace from an earlier one is skipped, and `ReferenceVoiceInfo.TryGetMoodClip` looks a mood up
  case-insensitively after trimming. Moods
  add no voice ids and do not change voice ordering.

**Error Handling**: Null, blank or unknown pack ids throw `ArgumentException` (a null store throws
`ArgumentNullException`); a disposed library throws `ObjectDisposedException` from `InstallPackAsync`;
cancellation propagates. Install failures are returned as a classified `SpeechModelDownloadResult`, not
thrown. Enumeration never creates folders and never throws: a missing folder is skipped silently, while a
caller-named folder that is not a valid path, or a folder that cannot be read (I/O or access failure), is
skipped with a `Warning` diagnostic naming it (and a mood folder that cannot be read yields no mood clips,
also with a `Warning`). It returns an empty list when nothing is present. An uninstall, or an emotion
upgrade whose installed folder cannot be moved aside, that fails because files are in use throws
`SpeechModelStoreException`; the previous install is kept.

**Disposal**: `Dispose` is idempotent and does not wait for in-flight installs. Each install registers as a
user of the downloader under the library lock before it starts, so the downloader is disposed by `Dispose`
when no install is running, or by the last running install when the library was disposed meanwhile; an install
never finds its downloader disposed under it. Installs started after disposal throw
`ObjectDisposedException`; `UninstallPackAsync`, `EnumerateVoices` and the state and path members need no
downloader and keep working. Pack operations assume a single process owns the store; two processes installing
the same pack concurrently are not coordinated.

**Consent notice**: The public constant `ConsentNotice` is the shared wording hosts show when listing
or choosing voices: only clone voices you have permission to use.

**Dependencies**: `SpeechModelStore`, `SpeechModelDownloader`, `IModelDownloadClient` (injected by tests),
`SpeechModelDownloadResult` (returned from installs), `ReferenceVoicePack`,
`ReferenceVoicePackInstaller`, `ReferenceVoiceInfo` and `ReferenceVoiceSource` (the voice values it returns;
defined with `ReferenceVoicePack`), `AudioFileReader.SupportedExtensions` (accepted clip extensions),
`ISpeechDiagnostics`.

**Callers**: The `ChatterboxTurbo` model and catalog extension, and hosts (the CLI and demo) that
install, remove or list voices.
