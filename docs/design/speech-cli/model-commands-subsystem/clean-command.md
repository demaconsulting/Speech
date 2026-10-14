### CleanCommand

**Purpose**: Implement `clean <modelId>`: best-effort removal of the partial-install and staging directories
that an interrupted or failed download left behind for one model.

**Data Model**: None; the single positional argument is the model id.

**Key Methods**:

- **RunAsync(context)**: Creates the catalog through `CliModelCatalogFactory`, wires `Ctrl+C` to a
  `CancellationTokenSource` through `ConsoleCancellation.Register` and awaits the overload below.
- **RunAsync(context, catalog, cancellationToken)**: Requires exactly one argument and awaits
  `catalog.CleanUpLeftoversAsync`. The library's cleanup is fully best-effort and never throws for a failed
  deletion, so the command needs no exception handling beyond cancellation, and a model with nothing to clean
  up is reported the same way as one whose leftovers were removed. An `OperationCanceledException` raised
  before or during the cleanup, while the command's own token is canceled, is reported with `WriteError`
  (`Cleaning up model '{id}' was canceled.`, non-zero exit code) and the command returns without propagating.

**Error Handling**: A missing or extra argument is an `ArgumentException` with no parameter name. A `null`
context or catalog is an `ArgumentNullException`.

**Dependencies**: `ICliModelCatalog`, `CliModelCatalogFactory`, `ConsoleCancellation` and `Context`.

**Callers**: `CommandDispatch`, through the `RunAsync(Context)` entry point.
