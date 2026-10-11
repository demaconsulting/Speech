### ReferenceVoiceLibrary

**Purpose**: One model-agnostic place for reference voices: install and remove downloadable packs,
keep a folder for the user's own clips, and list every available voice merged into one ordered,
de-duplicated list. Any voice-cloning model can consume it; `ChatterboxTurbo` is the first.

**Data Model**: A public sealed `IDisposable` constructed from a `SpeechModelStore` (typically
`catalog.Store`), optional `SpeechModelDownloaderOptions` (mirror) and optional `ISpeechDiagnostics`.
It owns a second `SpeechModelStore` rooted at `{store root}\reference-voices` and creates its
`SpeechModelDownloader` lazily. In-memory sets track pack ids installing or failed since process start.

Disk layout (the unit's contract with the CLI, demo and documentation):

```text
{models root}\reference-voices\
    user\                                  <- the user's own .wav/.flac clips (never touched by pack operations)
    voice-zero-1.0\
        install-manifest.json              <- written by SpeechModelStore
        current\
            pack.json  LICENSE.md  README.md
            voices\<id>.flac ...  voices\README.md
```

**Key Methods**: `RootPath`, `UserDirectory`, static `Packs`, `EnsureUserDirectory()`,
`GetPackDirectory(packId)`, `GetPackState(packId)`, `InstallPackAsync(packId, progress, ct)`,
`UninstallPack(packId)`, `EnumerateVoices(extraDirectories)`, `Dispose()`.

- `InstallPackAsync` calls the downloader's internal install-hook overload with the pack's descriptor
  and `ReferenceVoicePackInstaller.InstallAsync` as the hook, so the whole archive is fetched through
  the hash-verified, mirror-aware path (with a mirror the URL is
  `{mirror}/voice-zero-1.0/voice-zero-1.0.zip`), only a verified archive is extracted, and the result is
  swapped in atomically. A second call for an installed pack is a no-op returning `Installed`.
- `GetPackState` reports `Downloading`, `Downloaded`, `FailedOrCorrupt` (a failed attempt in this
  process) or `NotDownloaded`, and never throws for a known id.
- `UninstallPack` removes the pack only; the user folder is never touched.
- `EnumerateVoices` returns, in order, voices from the caller-named directories (in the order given),
  then the user folder, then each installed pack. Within a directory files are sorted by name
  (ordinal); `.wav` and `.flac` (any case) are accepted. The first voice with a given id (ordinal)
  wins and later duplicates are skipped with an `Info` diagnostic. The id is the file name without
  extension; the display name is a humanized form (`alan_davis_drake2` becomes `Alan Davis Drake 2`).
  Pack voices carry the pack's license and attribution.

**Error Handling**: Unknown pack ids throw `ArgumentException`; a disposed library throws
`ObjectDisposedException` from `InstallPackAsync`; cancellation propagates. Install failures are
returned as a classified `SpeechModelDownloadResult`, not thrown. Enumeration never creates folders
and never throws for a missing directory, and returns an empty list when nothing is present. An
uninstall that cannot complete because files are in use throws `SpeechModelStoreException`.

**Consent notice**: The public constant `ConsentNotice` is the shared wording hosts show when listing
or choosing voices: cloning a real person's voice requires that person's consent.

**Dependencies**: `SpeechModelStore`, `SpeechModelDownloader`, `ReferenceVoicePack`,
`ReferenceVoicePackInstaller`, `ISpeechDiagnostics`.

**Callers**: The `ChatterboxTurbo` model and catalog extension, and hosts (the CLI and demo) that
install, remove or list voices.
