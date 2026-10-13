### VoicesCommand

**Purpose**: Implement `voices packs`, `voices install <packId>` and `voices uninstall <packId>`: list, install
and remove reference-voice packs.

**Data Model**: None; the first argument selects the sub-action and the second is the pack id. The `--no-emotions`
global option is read from `Context.NoEmotions`.

**Key Methods**:

- **RunAsync(context)**: Creates the catalog through `CliModelCatalogFactory`, wires `Ctrl+C` to a
  `CancellationTokenSource` through `ConsoleCancellation.Register` and awaits the overload below.
- **RunAsync(context, catalog, cancellationToken)**: Dispatches on the first argument after checking the
  argument count for the sub-action.
- **packs**: Lists `catalog.VoicePacks` with each pack's state, read once per pack through `GetVoicePackState`.
  For an installed pack it also reports whether the pack was installed with emotions (a call to
  `VoicePackHasEmotionsAsync`). It shows the download size and the installed size without and with emotions in
  MiB, labeled `MiB` (from the pack's measured `VoicesBytes` and `EmotionsBytes`), the folder for the operator's own
  voices,
  and the consent note.
- **install `<packId>`**: Resolves the pack state first, so an unknown id gives the clean error before any
  output. Emotion variants are included by default and `--no-emotions` installs the base voices only; the
  command states which one it is doing and, when the pack is installed without emotions and emotions are
  requested, announces that it is removed and downloaded again. It then calls `InstallVoicePackAsync` with a
  reporter from `DownloadProgressReporter`. On success it reports the installed variant, and prints the consent
  note. Any other outcome is written with `WriteError`. Installed pack voices appear on the next invocation,
  because the voice list is read when the catalog is composed.
- **uninstall `<packId>`**: Awaits `UninstallVoicePackAsync`, a safe no-op when nothing is installed.

**Ctrl+C handling**: Every library call that can observe the token sits inside a `try` that catches
`OperationCanceledException` only when the command's own token is canceled. This covers the `packs` listing
(including the per-pack emotion check), the whole `install` body (the pre-check that detects an upgrade, the
download and the post-install emotion check) and the `uninstall` call. In each case the command reports the
cancellation with `WriteError` and a non-zero exit code and returns; none of them escapes as an unhandled
cancellation. An `OperationCanceledException` raised while the token is not canceled is not a user
cancellation, so it propagates unchanged. For `uninstall` the token is observed only before the removal starts.
The pack-id lookup before the `install` body is synchronous and cannot be canceled.

**Error Handling**: A missing sub-action, an unknown sub-action, a missing pack id, an extra argument, or an
unknown pack id is an `ArgumentException`. Argument-count errors carry parameter name `args`; the missing and
unknown sub-action errors carry no parameter name. A `SpeechModelStoreException` from `uninstall` is rethrown
as `InvalidOperationException`; `install` handles no store exception of its own, because the library reports
an install failure through the result outcome. A `null` context or catalog is an `ArgumentNullException`.

**Dependencies**: `ICliModelCatalog`, `CliModelCatalogFactory`, `DownloadProgressReporter`,
`ConsoleCancellation`, `Context` and the library's `ReferenceVoicePack`, `ReferenceVoiceLibrary` and download
result types.

**Callers**: `CommandDispatch`, through the `RunAsync(Context)` entry point.
