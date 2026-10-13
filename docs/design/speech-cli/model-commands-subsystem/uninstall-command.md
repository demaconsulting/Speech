### UninstallCommand

**Purpose**: Implement `uninstall <modelId>`: remove one model's installed content, manifest and leftover
scratch directories.

**Data Model**: None; the single positional argument is the model id.

**Key Methods**:

- **RunAsync(context)**: Creates the catalog through `CliModelCatalogFactory`, wires `Ctrl+C` to a
  `CancellationTokenSource` through `ConsoleCancellation.Register` and awaits the overload below.
- **RunAsync(context, catalog, cancellationToken)**: Requires exactly one argument and awaits
  `catalog.UninstallAsync`. Uninstalling a model with nothing installed is a safe no-op in the library, so the
  command reports success in that case too: an idempotent cleanup script must not fail because it ran twice.
  The token is observed before the removal starts; a cancellation then is reported with `WriteError`
  (`Uninstalling model '{id}' was canceled.`, non-zero exit code), the model stays installed, and the command
  returns without propagating.

**Error Handling**: A missing or extra argument is an `ArgumentException` with no parameter name. A genuine
store failure (`SpeechModelStoreException`, for example an open file handle that prevents deletion) is caught
and rethrown as `InvalidOperationException`, so `Program.Main`'s clean-error convention (a one-line message and
a non-zero exit code) applies instead of a stack trace. A `null` context or catalog is an
`ArgumentNullException`.

**Dependencies**: `ICliModelCatalog`, `CliModelCatalogFactory`, `ConsoleCancellation` and `Context`.

**Callers**: `CommandDispatch`, through the `RunAsync(Context)` entry point.
