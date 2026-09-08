## SpeechCli ModelCommandsSubsystem Design

![ModelCommandsSubsystem Structure](ModelCommandsSubsystemView.svg)

### Overview

The ModelCommandsSubsystem implements the five model-management subcommands dispatched to by
`CommandDispatch`: `list-models`, `model-info`, `download`, `uninstall`, and `clean`. It contains
one command handler per subcommand, plus one shared catalog seam used by all five:

- **`ICliModelCatalog`** / **`SpeechModelCatalogAdapter`**: the CLI-owned seam over the library's
  `SpeechModelCatalog`/`SpeechModelStore`, and its real implementation
- **`CliModelCatalogFactory`**: composes a `SpeechModelCatalogAdapter` honoring `--models-dir`
- **`ListModelsCommand`**, **`ModelInfoCommand`**, **`DownloadCommand`**, **`UninstallCommand`**,
  **`CleanCommand`**: one handler per subcommand

### Why a CLI-Owned Seam

`SpeechModelCatalog`'s test-friendly constructor (accepting an injected known-model list, store,
and download client) is `internal`, and the library grants `InternalsVisibleTo` only to its own
test projects, not to `DemaConsulting.Speech.Cli.Tests`. Rather than widen that internal access
purely to make the CLI testable — which would weaken the evidence the library's own tests
provide — the CLI owns a narrow seam interface, mirroring the pattern SpeechDemo already
established for the same reason (`IModelCatalogService`/`ModelCatalogService`):

| Member | Returns | Behavior |
| --- | --- | --- |
| `Enumerate()` | `IReadOnlyList<SpeechModelDescriptor>` | Never throws |
| `DownloadAsync(id, progress, token)` | `Task<SpeechModelDownloadResult>` | Throws for unknown id; no-op if installed |
| `Uninstall(id)` | `void` | Safe no-op if nothing is installed |
| `CleanUpLeftovers(id)` | `void` | Never throws |

`SpeechModelCatalogAdapter` is the only production implementation. It owns (and disposes) a real
`SpeechModelCatalog`, delegating `Uninstall`/`CleanUpLeftovers` through the catalog's `Store`.
`CliModelCatalogFactory.Create(context)` composes one adapter per command invocation, passing
`context.ModelsDir` through `SpeechModelStoreOptions.RootPathOverride` when given, so `--models-dir`
is honored uniformly by every command without each command handler needing to know how the
override is threaded through.

### ListModelsCommand

Parses `--role tts`/`stt` (mapping to `SpeechModelRole.Synthesis`/`Recognition`) and
`--state downloaded`/`missing` (mapping to `State == Downloaded`/`State != Downloaded`, the
latter covering `NotDownloaded`, `Downloading`, and `FailedOrCorrupt` alike, since only two state
groups are exposed at the CLI). Filters `catalog.Enumerate()` by whichever flags were given, then
prints either an aligned table (columns: Id, Display Name, Role, State, License) or, with
`--format json`, an indented JSON array via `System.Text.Json`. An empty result (whether because
the catalog has no known models, or because a filter excluded everything) prints an explanatory
"no models" message rather than an empty table with only headers.

### ModelInfoCommand

Looks up the requested `<modelId>` in `catalog.Enumerate()`; an id not found throws
`ArgumentException` reading `Unknown model id '{id}'. Use 'list-models' to see available
models.`, which `Program.Main`'s clean-error convention turns into a one-line message and a
non-zero exit code rather than a stack trace. A found model's identity, state, license
name/URL, and audio-tag support are printed, followed by every one of its `ISpeechModelParameter`
entries, switched on concrete type: a `NumericParameter` prints its min/max/step/default/unit, a
`ChoiceParameter` prints its option list and default, and a `BooleanParameter` prints its default.

### DownloadCommand

Parses one or more positional `<modelId>` values plus the optional `--force` flag, then downloads
each requested model **sequentially** (never in parallel, so progress output from different
models is never interleaved). For each model:

1. If `--force` was given and the model's current state (per `Enumerate()`) is `Downloaded`,
   `Uninstall` is called first — this is necessary because `SpeechModelCatalog.DownloadAsync` is
   itself documented as a no-op for an already-installed model, so there is no separate
   "force" parameter on the library's own download API to pass through
2. `DownloadAsync` is called with an `IProgress<SpeechModelDownloadProgress>` that prints a
   throttled percent-complete (or, when the transfer size is unknown, kilobytes-transferred)
   line, so it never floods the console with one line per chunk
3. A successful (`Installed`) outcome is reported and the loop continues; any other outcome
   (`ChecksumMismatch`, transport failure) or a thrown exception (including an unknown model id)
   is reported as an error for that model only, and the loop still continues to the next
   requested model — a single bad id must not abandon the rest of a multi-model request, mirroring
   the CI `ModelDownloader` tool's own per-model failure isolation

`Ctrl+C` is wired to a `CancellationTokenSource` via `Console.CancelKeyPress` (which sets
`e.Cancel = true` so the runtime does not kill the process outright, then cancels the token
cooperatively). Unlike any other per-model failure, a genuine cancellation **stops the whole
batch**: the loop checks the token before starting each model and treats `OperationCanceledException`
as "stop", not "report and continue", because an operator interrupting the process is asking the
whole operation to end, not just the model currently downloading. Any requested model that fails
for any reason still causes the process to exit non-zero, per the CI tool's existing convention.

### UninstallCommand

Calls `catalog.Uninstall(modelId)` and reports success. `SpeechModelStore.Uninstall` is documented
as a safe no-op for a model with nothing installed, so this command follows that same contract —
"nothing to remove" is a successful outcome, not an error, since an idempotent cleanup script
should not fail merely because it ran twice. A genuine store failure
(`SpeechModelStoreException`, e.g. an open file handle preventing deletion) is caught and
re-thrown as `InvalidOperationException`, so `Program.Main`'s clean-error convention (a one-line
message, non-zero exit) applies instead of a stack trace, matching how the rest of the CLI
reports operator-facing failures.

### CleanCommand

Calls `catalog.CleanUpLeftovers(modelId)` and reports success. `SpeechModelStore.CleanUpLeftovers`
is documented as fully best-effort and never throwing, so this handler needs no exception
handling of its own; a model with nothing to clean up is reported the same way as one with
leftovers actually removed.

### Interactions with Other Units

Every command depends only on `ICliModelCatalog` and on the library's descriptor, state,
parameter, progress, and result value types — none touches `SpeechModelCatalog` or
`SpeechModelStore` directly. This is what allows the full argument-parsing, filtering, error, and
progress-reporting behavior of all five commands to be verified deterministically with no network
access and no real model download, using a fake `ICliModelCatalog` (see each command's own test
file under `test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/`).

### Addendum (Pass 5): Seam Extension for SynthesisCommandSubsystem

Pass 5's `speak` subcommand needs to construct a real `ISpeechSynthesizer`, which requires casting
a resolved model to the library's internal `ISynthesisModel` interface - a cast that is only
compilable inside this same assembly (`DemaConsulting.Speech.Cli`), not inside
`DemaConsulting.Speech.Cli.Tests`, which is not granted `InternalsVisibleTo` access to it. Rather
than introduce a second, competing seam interface for `SynthesisCommandSubsystem`, `ICliModelCatalog`
is extended with two further members that keep the same shape as the original five - a narrow,
purpose-specific operation, never throwing for a genuinely absent capability where the library
itself would return a graceful fallback:

| Member | Returns | Behavior |
| --- | --- | --- |
| `GetPreferredAudioFormat(descriptor)` | `AudioFormat` | Throws for a non-synthesis model |
| `CreateSynthesizer(descriptor, device, values)` | `ISpeechSynthesizer` | Throws for a non-synthesis model |

`SpeechModelCatalogAdapter` implements both through a private `RequireSynthesisModel` helper that
performs the `is ISynthesisModel` cast once, throwing a clean, model-id-naming `ArgumentException`
when it fails. `CreateSynthesizer`'s implementation forwards to the library's public
`SpeechSynthesizerFactory.Create(ISynthesisModel, SpeechModelCatalog, IAudioPlaybackDevice,
ISpeechDiagnostics?, IReadOnlyDictionary<string,object>?)` overload, resolving the model's
installed directory internally via the adapter's own composed catalog. See _SpeechCli
SynthesisCommandSubsystem Design_ for how `SpeakCommand` consumes these two members.
