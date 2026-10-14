### ModelInfoCommand

**Purpose**: Implement `model-info <modelId>`: print one known model's identity, install state, license,
audio-tag support and every declared parameter.

**Data Model**: None beyond the descriptor it prints. Parameters are `ISpeechModelParameter` values switched on
their concrete type.

**Key Methods**:

- **Run(context)**: Creates the catalog through `CliModelCatalogFactory` and delegates to the overload below.
- **Run(context, catalog)**: Requires exactly one positional argument, looks it up in `catalog.Enumerate()` by
  ordinal id comparison and writes the model. The output lists id, display name, role, state, license name
  (followed by the URL in parentheses only when there is one; a model without a license URL prints the name alone) and
  audio-tag support, then each parameter: a `NumericParameter` prints its
  kind (integer or numeric), min, max, step, default and unit, formatted with the invariant culture so the
  decimal separator is a period on every machine; a `ChoiceParameter` prints its options and default; a
  `BooleanParameter` prints its default. A model with no parameters prints `Parameters: (none)`.

**Error Handling**: A missing id, an extra argument, or an id that matches no known model is an
`ArgumentException`; the unknown-id message reads `Unknown model id '{id}'. Use 'list-models' to see available
models.` These exceptions carry no parameter name, because the fault is in the operator's input and not in a
named method parameter. `Program.Main` turns them into a one-line message and a non-zero exit code rather than
a stack trace. A `null` context or catalog is an `ArgumentNullException`.

**Dependencies**: `ICliModelCatalog`, `CliModelCatalogFactory`, `Context` and the library's descriptor and
parameter types.

**Callers**: `CommandDispatch`, through the `Run(Context)` entry point.
