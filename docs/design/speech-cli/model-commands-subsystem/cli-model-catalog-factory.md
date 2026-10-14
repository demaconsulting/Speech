### CliModelCatalogFactory

**Purpose**: Compose one `SpeechModelCatalogAdapter` per command invocation, honoring the global options that
affect where models and voices live and where downloads come from, so that no command handler needs to know
how those options are threaded into the library.

**Data Model**: A static class with no state.

**Key Methods**:

- **Create(context)**: Builds `SpeechModelStoreOptions` with `RootPathOverride` set to `context.ModelsDir` when
  one was given (otherwise passes no options, so the library's per-user store root applies). Calls
  `context.CreateMirror()` and, when a mirror is configured, wraps it in `SpeechModelDownloaderOptions` so
  `--mirror-url`, `--mirror-user`, `--mirror-password` and `--mirror-bearer-token` redirect downloads to the
  internal mirror. Passes `context.VoicesDir` as the explicit reference-voice directory. The caller owns the
  returned adapter and disposes it.

**Error Handling**: A `null` context is an `ArgumentNullException`. Validation of the mirror options (mutually
exclusive credential forms, the `https` requirement outside loopback) is performed by `Context.CreateMirror`
and surfaces here as the `ArgumentException` that method throws; the factory adds no validation of its own.
The system-level mirror requirements `SpeechCli-Cli-DownloadMirrorCredentialConflict` and
`SpeechCli-Cli-DownloadMirrorBearerTokenValidation` are enforced centrally in that method before any download
starts.

**Dependencies**: `Context` (for `ModelsDir`, `VoicesDir` and `CreateMirror`), `SpeechModelStoreOptions`,
`SpeechModelDownloaderOptions` and `SpeechModelCatalogAdapter`.

**Callers**: The public entry point of every model and voice command (`list-models`, `model-info`, `download`,
`uninstall`, `clean`, `list-voices`, `voices`), and `SpeakCommand`, `RecognizeCommand`, `AskCommand` and
`DoctorCommand`.
