### ReferenceVoiceLibrary

**Purpose**: One model-agnostic place for reference voices: install and remove downloadable packs,
keep a folder for the user's own clips, and list every available voice merged into one ordered,
de-duplicated list. Any voice-cloning model can consume it; `ChatterboxTurbo` is the first.

**Data Model**: A public sealed `IDisposable` constructed from a `SpeechModelStore` (typically
`catalog.Store`), optional `SpeechModelDownloaderOptions` (mirror) and optional `ISpeechDiagnostics`.
It owns a second `SpeechModelStore` rooted at `{store root}\{RootDirectoryName}` (the public constant
`RootDirectoryName`, `reference-voices`; `UserDirectoryName` is `user`) and creates its
`SpeechModelDownloader` lazily. In-memory sets track pack ids installing or failed since process start.

Disk layout (the unit's contract with the CLI, demo and documentation):

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
`InstallPackAsync(packId, progress, ct)` overload that includes emotions), `UninstallPack(packId)`,
`PackHasEmotions(packId)`, `GetPackInstalledVariants(packId)`, `EnumerateVoices(extraDirectories)`, `Dispose()`.

- `InstallPackAsync` calls the downloader's internal install-hook overload with the pack's descriptor
  and `ReferenceVoicePackInstaller.InstallAsync` as the hook, so the whole archive is fetched through
  the hash-verified, mirror-aware path (with a mirror the URL is
  `{mirror}/voice-zero-1.0/voice-zero-1.0.zip`), only a verified archive is extracted, and the result is
  swapped in atomically. A second call for an installed pack is a no-op returning `Installed`.
- `includeEmotions` (default `true`) also extracts the emotion clips (see `ReferenceVoicePackInstaller`).
  **Upgrade**: when emotions are requested and the installed pack lacks them, the library renames
  `current` to a sibling `current.upgrade-backup-{guid}` folder (the downloader treats an installed id
  as complete and would otherwise skip the download) and installs again. A `finally` deletes the backup
  on an `Installed` outcome and renames it back on any other outcome or exception, so a failed or
  cancelled re-download keeps the previous install; a leftover backup from a crash is restored (or
  deleted when `current` exists) at the start of the next install. A rename that fails because files are
  in use throws `SpeechModelStoreException`. **Concurrency**: whole `InstallPackAsync` calls for one pack
  are serialized by a per-pack `SemaphoreSlim` (taken before the upgrade check), and the in-progress
  state is reference counted so `GetPackState` reports `Downloading` until the last concurrent caller
  finishes. An install that already has emotions is never downgraded by `includeEmotions: false`,
  and a voices-only request over a voices-only install is the usual no-op.
- `GetPackInstalledVariants` reads `pack.json` and returns its `variants` (empty for a missing, unreadable
  or malformed file; it never throws); `PackHasEmotions` is true when `voices-emotion` is among them.
- `GetPackState` reports `Downloading`, `Downloaded`, `FailedOrCorrupt` (a failed attempt in this
  process) or `NotDownloaded`, and never throws for a known id.
- `UninstallPack` removes the pack only; the user folder is never touched. It takes the same
  per-pack lock as `InstallPackAsync` without waiting; if an install of that pack is in flight it
  throws `SpeechModelStoreException` and changes nothing, so callers retry after the install ends.
  After the store uninstall it also deletes every `current.upgrade-backup-*` folder left in the pack
  directory so a leftover backup cannot resurrect the pack, and clears the pack's failed flag.
- `EnumerateVoices` returns, in order, voices from the caller-named directories (in the order given),
  then the user folder, then each installed pack. Within a directory files are sorted by name
  (ordinal); the extensions in `AudioFileReader.SupportedExtensions` (`.wav` and `.flac`, any case) are
  accepted. The first voice with a given id (ordinal)
  wins and later duplicates are skipped with an `Info` diagnostic. The id is the file name without
  extension; the display name is a humanized form (`alan_davis_drake2` becomes `Alan Davis Drake 2`).
  Pack voices carry the pack's license and attribution. For a pack installed with emotions, each pack
  voice's `MoodClips` maps the lower-case mood (file stem) to the absolute path of
  `voices-emotion\{voiceId}\{mood}.{ext}` (the shipped pack uses `.flac`; any supported extension is
  found), ordinally sorted, or is `null` when there are none. Moods
  add no voice ids and do not change voice ordering.

**Error Handling**: Unknown pack ids throw `ArgumentException`; a disposed library throws
`ObjectDisposedException` from `InstallPackAsync`; cancellation propagates. Install failures are
returned as a classified `SpeechModelDownloadResult`, not thrown. Enumeration never creates folders
and never throws for a missing directory, and returns an empty list when nothing is present. An
uninstall that cannot complete because files are in use throws `SpeechModelStoreException`.

**Consent notice**: The public constant `ConsentNotice` is the shared wording hosts show when listing
or choosing voices: cloning a real person's voice requires that person's consent.

**Dependencies**: `SpeechModelStore`, `SpeechModelDownloader`, `ReferenceVoicePack`,
`ReferenceVoicePackInstaller`, `AudioFileReader.SupportedExtensions` (accepted clip extensions),
`ISpeechDiagnostics`.

**Callers**: The `ChatterboxTurbo` model and catalog extension, and hosts (the CLI and demo) that
install, remove or list voices.
