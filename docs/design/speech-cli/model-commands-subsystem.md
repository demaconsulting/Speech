## SpeechCli ModelCommandsSubsystem Design

![ModelCommandsSubsystem Structure](ModelCommandsSubsystemView.svg)

### Overview

The ModelCommandsSubsystem implements the five model-management subcommands dispatched to by
`CommandDispatch` (`list-models`, `model-info`, `download`, `uninstall`, and `clean`) and the two
reference-voice subcommands `list-voices` and `voices`. It contains one command handler per
subcommand, plus one shared catalog seam used by all of them:

- **`ICliModelCatalog`** / **`SpeechModelCatalogAdapter`**: the CLI-owned seam over the library's
  `SpeechModelCatalog`/`SpeechModelStore`, and its real implementation
- **`CliModelCatalogFactory`**: composes a `SpeechModelCatalogAdapter` honoring `--models-dir` and
  `--mirror-*`
- **`ListModelsCommand`**, **`ModelInfoCommand`**, **`DownloadCommand`**, **`UninstallCommand`**,
  **`CleanCommand`**: one handler per model-management subcommand
- **`ListVoicesCommand`**, **`VoicesCommand`**: the reference-voice handlers (see below)

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
`SpeechModelCatalog`, constructed as `new SpeechModelCatalog(options).AddSherpaModels()` -
populating the catalog via the sibling `DemaConsulting.Speech.Sherpa` package's extension method,
since core `DemaConsulting.Speech` ships zero built-in models - and delegating
`Uninstall`/`CleanUpLeftovers` through the catalog's `Store`.
`CliModelCatalogFactory.Create(context)` composes one adapter per command invocation, passing
`context.ModelsDir` through `SpeechModelStoreOptions.RootPathOverride` when given, so `--models-dir`
is honored uniformly by every command without each command handler needing to know how the
override is threaded through. `context.CreateMirror()` is wrapped in a
`SpeechModelDownloaderOptions` the same way, so `--mirror-url`/`--mirror-user`/`--mirror-password`/
`--mirror-bearer-token` redirect `DownloadCommand`'s downloads to an internal mirror without any
command handler needing mirror-specific logic of its own (see `CreateMirror`'s own validation of
the mutually-exclusive credential/bearer-token combinations, and the loopback-only exemption from
requiring `https://`, in `Context`'s own source).

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

### ListVoicesCommand

Parses the optional `--format table|json` flag (anything else is an `ArgumentException`) and prints
`catalog.EnumerateVoices()` as an aligned table (Id, Source, Display Name) or an indented JSON
array. When there are no voices it explains how to obtain some (`voices install voice-zero-1.0`,
the per-user folder `catalog.UserVoicesDirectory`, or `--voices-dir`). The table form also shows
how to choose a voice (`speak --tts-param voice=<id>`) and the reference-voice consent note
(`ReferenceVoiceLibrary.ConsentNotice`: only clone voices you have permission to use).

### VoicesCommand

Dispatches on the first argument:

- **`packs`** lists `catalog.VoicePacks` with each pack's `GetVoicePackState` and the consent note
- **`install <packId>`** calls `InstallVoicePackAsync` with an `IProgress` that prints throttled
  percent (or KB) lines, exactly as `DownloadCommand` does, and prints the consent note on success.
  A failed or canceled install is reported with `WriteError` and a non-zero exit code. `Ctrl+C` is
  wired to a `CancellationTokenSource` the same way as `DownloadCommand`
- **`uninstall <packId>`** calls `UninstallVoicePack`, a safe no-op when nothing is installed; a
  `SpeechModelStoreException` is re-thrown as `InvalidOperationException`

An unknown pack id, a missing or extra argument, or an unknown action is an `ArgumentException`.
Because the catalog is built from the same `--models-dir` and `--mirror-*` options, pack downloads
honor a mirror (`{mirror}/voice-zero-1.0/voice-zero-1.0.zip`) without any command-specific logic.
Installed pack voices appear on the next invocation, since the voice list is fixed when the
catalog (and the Chatterbox model) is constructed.

### Chatterbox and Voice Seam Members

`SpeechModelCatalogAdapter` builds `.AddSherpaModels().AddKokoroModels().AddNemotronSttModels()`,
creates one `ReferenceVoiceLibrary` over the catalog's store and the mirror-aware downloader
options, and then calls `AddChatterboxTurboModels` with `ReferenceVoiceDirectory = --voices-dir`
and that same library, so the model and the voice commands see identical voices. `ICliModelCatalog`
gains `VoicePacks`, `UserVoicesDirectory`, `EnumerateVoices()`, `GetVoicePackState`,
`InstallVoicePackAsync`, and `UninstallVoicePack`. Voice choice itself uses the model's existing
generic `voice` choice parameter (`speak --tts-param voice=<id>`). Voices from an explicit
`--voices-dir` are decoded when the catalog is built, so an undecodable file there fails every
command that builds the catalog; pack and user-folder voices are decoded lazily.

### Interactions with Other Units

Every command depends only on `ICliModelCatalog` and on the library's descriptor, state,
parameter, progress, and result value types — none touches `SpeechModelCatalog` or
`SpeechModelStore` directly. This is what allows the full argument-parsing, filtering, error, and
progress-reporting behavior of all seven commands to be verified deterministically with no network
access and no real model download, using a fake `ICliModelCatalog` (see each command's own test
file under `test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/`).

### Addendum (Pass 5): Seam Extension for SynthesisCommandSubsystem and RecognitionCommandSubsystem

`speak` needs to load a real `ISpeechSynthesizerEngine`, and `recognize` needs to load a real
`ISpeechRecognizerEngine`, each of which requires casting a resolved model to the library's fully
public `ISynthesisModel`/`IRecognitionModel` interface - both interfaces are implementable (and
their `is`/`as` casts compilable) from any assembly, including `DemaConsulting.Speech.Cli.Tests`.
Rather than introduce competing seam interfaces for each subsystem, `ICliModelCatalog` is extended
with four further members that keep the same shape as the original five - narrow,
purpose-specific operations, never throwing for a genuinely absent capability where the library
itself would return a graceful fallback - so the role-specific narrowing stays confined to one
seam instead of being repeated across every caller:

| Member | Returns | Behavior |
| --- | --- | --- |
| `GetPreferredAudioFormat(descriptor)` | `AudioFormat` | Throws for a non-synthesis model |
| `CreateSynthesizerEngineAsync(...)` | `Task<ISpeechSynthesizerEngine>` | Non-synthesis model throws; no device bound |
| `GetAudioFormat(descriptor)` | `AudioFormat` | Throws for a non-recognition model |
| `CreateRecognizerEngineAsync(...)` | `Task<ISpeechRecognizerEngine>` | Non-recognition model throws; no device bound |

`SpeechModelCatalogAdapter` implements all four through private `RequireSynthesisModel`/
`RequireRecognitionModel` helpers that perform the respective `is ISynthesisModel`/
`is IRecognitionModel` cast once, throwing a clean, model-id-naming `ArgumentException` when it
fails. `CreateSynthesizerEngineAsync`/`CreateRecognizerEngineAsync` forward unchanged to the
library's `SpeechSynthesizerFactory.LoadAsync`/`SpeechRecognizerFactory.LoadAsync` static methods,
each returning an engine with no device bound yet; the caller (`SpeakCommand`/`RecognizeCommand`/
`AskCommand`) binds the resolved playback/capture device afterward via
`engine.CreateSessionAsync(device, cancellationToken)`. See _SpeechCli SynthesisCommandSubsystem
Design_ and _SpeechCli RecognitionCommandSubsystem Design_ for how each command consumes its two
members.
