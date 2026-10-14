### DownloadCommand

**Purpose**: Implement `download <modelId> [<modelId>...] [--force]`: download, verify and install one or more
known models, one after another, with live progress.

**Data Model**: The parser returns the ordered list of model ids and the `--force` flag; flags may appear in
any order relative to the ids.

**Key Methods**:

- **RunAsync(context)**: Creates the catalog through `CliModelCatalogFactory`, wires `Ctrl+C` to a
  `CancellationTokenSource` through `ConsoleCancellation.Register` (which suppresses default termination, so
  the store's own staging and abandon logic can clean a partially staged download), and awaits the overload
  below.
- **RunAsync(context, catalog, cancellationToken)**: Downloads the requested models **sequentially**, never in
  parallel, so progress from different models is never interleaved. Before each model it checks the token; if
  already canceled, it reports that the download was canceled before it started and stops. Each model is then
  attempted in a per-model step with this ordering:
  1. With `--force`, the model's current state is read from `Enumerate()`. Only when it is `Downloaded` is
     `UninstallAsync` called first, because `SpeechModelCatalog.DownloadAsync` is a no-op for an installed
     model and the library has no force parameter. Any leftovers of a model that is not `Downloaded` are not
     touched. An id that matches no model finds no descriptor, so nothing is uninstalled and the failure
     comes from the next step.
  2. `DownloadAsync` runs with a reporter from `DownloadProgressReporter`.
  3. An `Installed` outcome is reported and the loop continues. Any other outcome (`ChecksumMismatch`,
     transport failure) is reported as an error for that model only, and the loop continues.
- An `ArgumentException` (such as an unknown id) or `InvalidOperationException` (such as a store failure) is
  reported as an error for that model only and the loop continues, so a single bad id never abandons the rest
  of a multi-model request. These two exception types are the deliberate per-model isolation boundary; any
  other exception (for example an `IOException`) is unexpected, is not caught, and propagates to the caller
  so a real fault is never mislabeled as a per-model failure.
- A cancellation is the one exception to continue-on-failure. An `OperationCanceledException` raised by the
  `--force` uninstall or by the download is reported as `Download of '{id}' was canceled.` (a clean message, never an
  unhandled exception) and stops the whole
  batch, because an operator who interrupts the process is asking the whole operation to end. An unknown id
  never raises a cancellation, so the unknown-id path is independent of the token.
- Every error is written with `Context.WriteError`, so any failed or canceled model makes the process exit
  non-zero.

The mirror options are applied centrally when the catalog is composed, so the command has no mirror-specific
logic. The system-level requirements `SpeechCli-Cli-DownloadMirrorCredentialConflict` and
`SpeechCli-Cli-DownloadMirrorBearerTokenValidation` are enforced there before any download starts; the system
design covers the details.

**Error Handling**: No model id, or an unsupported `--` flag, is an `ArgumentException` with parameter name
`args`, raised before anything is downloaded. A `null` context or catalog is an `ArgumentNullException`.
Per-model `ArgumentException` and `InvalidOperationException` failures never propagate; they are written as errors as
described above. Any other exception propagates.

**Dependencies**: `ICliModelCatalog`, `CliModelCatalogFactory`, `DownloadProgressReporter`,
`ConsoleCancellation` and `Context`.

**Callers**: `CommandDispatch`, through the `RunAsync(Context)` entry point.
