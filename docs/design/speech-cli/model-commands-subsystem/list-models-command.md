### ListModelsCommand

**Purpose**: Implement `list-models`: list every known model, optionally filtered by role and install state,
as an aligned table or as JSON.

**Data Model**: A private options record holds the parsed role, the two state flags and the output format. A
private row record carries the five JSON fields `Id`, `DisplayName`, `Role`, `State` and `LicenseName`, which
are the same fields the table shows (Id, Display Name, Role, State, License).

**Key Methods**:

- **Run(context)**: Creates the catalog through `CliModelCatalogFactory` and delegates to the overload below.
- **Run(context, catalog)**: Parses the flags, filters `catalog.Enumerate()` and writes the result.
  `--role tts` or `stt` maps to `SpeechModelRole.Synthesis` or `Recognition`. `--state downloaded` keeps models
  whose state is `Downloaded`; `--state missing` keeps every other state (`NotDownloaded`, `Downloading`,
  `FailedOrCorrupt`), because only two state groups are exposed at the command line. `--format table|json`
  selects an aligned table (the default) or an indented JSON array.
- An empty result, whether the catalog is empty or a filter excluded everything, prints an explanatory "no
  models" line in table form rather than a header-only table.

**Error Handling**: An unknown flag, an invalid `--role`, `--state` or `--format` value, a flag with no value,
or contradictory `--state downloaded` and `--state missing` options (in either order) is an `ArgumentException`
with parameter name `args`, which `Program.Main` reports as a one-line message with a non-zero exit code. A
`null` context or catalog is an `ArgumentNullException`.

**Dependencies**: `ICliModelCatalog`, `CliModelCatalogFactory`, `Context`, the library's `SpeechModelDescriptor`,
`SpeechModelRole` and `SpeechModelState`, and `System.Text.Json`.

**Callers**: `CommandDispatch`, through the `Run(Context)` entry point.
