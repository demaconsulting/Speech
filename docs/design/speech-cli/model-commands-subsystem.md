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

### Interfaces

**Exposed**: each command exposes a static entry point invoked from the `CommandDispatch` table:
`Run(Context)` for the synchronous commands (`list-models`, `model-info`, `list-voices`) and
`RunAsync(Context)` for `DownloadCommand`, `UninstallCommand`, `CleanCommand` and `VoicesCommand`, which await
their asynchronous work. `ICliModelCatalog` is the
subsystem's public-to-the-CLI seam, and `CliModelCatalogFactory.Create(context)` composes the real
implementation. Errors for operator mistakes are `ArgumentException` or
`InvalidOperationException`, which `Program.Main` reports as a one-line message and exit code `1`.

**Consumed**: the library's `SpeechModelCatalog`, `SpeechModelStore`, `SpeechModelDownloaderOptions`,
`SpeechModelDescriptor`, parameter, progress and result types, `ReferenceVoiceLibrary`,
`SpeechSynthesizerFactory`, `SpeechRecognizerFactory`, and the `AddSherpaModels`/`AddKokoroModels`/
`AddNemotronSttModels`/`AddChatterboxTurboModels` registration extensions; and `Context` for
global options (`--models-dir`, `--voices-dir`, `--mirror-*`, `--no-emotions`).

The `ICliModelCatalog` members fall into four groups:

| Group | Members |
| --- | --- |
| Model management (4) | `Enumerate`, `DownloadAsync`, `UninstallAsync`, `CleanUpLeftoversAsync` |
| Format and engine (3) | `GetPreferredAudioFormat`, `CreateSynthesizerEngineAsync`, `CreateRecognizerEngineAsync` |
| Reference voices: browse (4) | `VoicePacks`, `UserVoicesDirectory`, `EnumerateVoices`, `GetVoicePackState` |
| Reference voices: manage (3) | `VoicePackHasEmotionsAsync`, `InstallVoicePackAsync`, `UninstallVoicePackAsync` |
| Format and engine (3) | `GetPreferredAudioFormat`, `CreateSynthesizerEngineAsync`, `CreateRecognizerEngineAsync` |
| Reference voices: browse (4) | `VoicePacks`, `UserVoicesDirectory`, `EnumerateVoices`, `GetVoicePackState` |
| Reference voices: manage (3) | `VoicePackHasEmotionsAsync`, `InstallVoicePackAsync`, `UninstallVoicePackAsync` |

### Design

The seven commands are independent handlers that share a single `ICliModelCatalog` created per
invocation by `CliModelCatalogFactory`; no command touches `SpeechModelCatalog` or
`SpeechModelStore` directly. `SpeechModelCatalogAdapter` owns and disposes the real catalog, and
the commands, `speak`, `recognize`, `ask`, and `doctor` all consume the same seam. The sections
below describe the seam and each unit.

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
| `UninstallAsync(id, token)` | `Task` | `ArgumentException` for bad id; no-op if absent; `SpeechModelStoreException` on failure |
| `CleanUpLeftoversAsync(id, token)` | `Task` | `ArgumentException` for bad id; else best-effort, never throws |

These four model-management members are the original seam; the format/engine members (see _Seam
Extension for SynthesisCommandSubsystem and RecognitionCommandSubsystem_ below) and the
reference-voice members (see _Chatterbox and Voice Seam Members_) extend it.

`SpeechModelCatalogAdapter` is the only production implementation. It owns (and disposes) a real
`SpeechModelCatalog`, constructed as `new SpeechModelCatalog(options).AddSherpaModels()` -
populating the catalog via the sibling `DemaConsulting.Speech.Sherpa` package's extension method,
since core `DemaConsulting.Speech` ships zero built-in models - and delegating
`UninstallAsync`/`CleanUpLeftoversAsync` to the library store's own `Uninstall`/`CleanUpLeftovers`
methods through the catalog's `Store`.
`CliModelCatalogFactory.Create(context)` composes one adapter per command invocation, passing
`context.ModelsDir` through `SpeechModelStoreOptions.RootPathOverride` when given and
`context.VoicesDir` as the explicit reference-voice directory, so `--models-dir` and `--voices-dir`
are honored uniformly by every command without each command handler needing to know how the
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
groups are exposed at the CLI; two contradictory `--state` values are an `ArgumentException`
rather than a silently empty result). Filters `catalog.Enumerate()` by whichever flags were given, then
prints either an aligned table (columns: Id, Display Name, Role, State, License) or, with
`--format json`, an indented JSON array via `System.Text.Json`. An unknown flag, an invalid
`--role`/`--state`/`--format` value, or a flag missing its value is an `ArgumentException`. An empty result (whether because
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
   `UninstallAsync` is called first — this is necessary because `SpeechModelCatalog.DownloadAsync` is
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

`Ctrl+C` is wired to a `CancellationTokenSource` through the shared `ConsoleCancellation.Register`
helper (see _SpeechCli System Design_), which sets `e.Cancel = true` so the runtime does not kill
the process outright, then cancels the token
cooperatively. Unlike any other per-model failure, a genuine cancellation **stops the whole
batch**: the loop checks the token before starting each model and treats `OperationCanceledException`
as "stop", not "report and continue", because an operator interrupting the process is asking the
whole operation to end, not just the model currently downloading. Any requested model that fails
for any reason still causes the process to exit non-zero, per the CI tool's existing convention.

### DownloadProgressReporter

`DownloadProgressReporter.Create(context, describe)` builds the throttled live progress reporter
shared by `DownloadCommand` and `VoicesCommand`: it prints one `{label}: {percent}%` line per
whole percent (or `{label}: {n} KB` per whole kilobyte when the total size is unknown) and
suppresses repeats, so it never prints one line per transferred chunk. The caller supplies the
label (`'id' file i/n` for `download`, `'id'` for `voices install`), so the two commands keep one
throttling implementation instead of two copies.

### Asynchronous execution

The seam's uninstall, cleanup and pack-query members are asynchronous (`Task`, with a `CancellationToken`);
`UninstallCommand`, `CleanCommand`, `DownloadCommand` (the `--force` uninstall) and `VoicesCommand` await them in
their internal `RunAsync` bodies. The public `RunAsync(Context)` entry of each of these commands wires `Ctrl+C` to a
`CancellationTokenSource` and awaits the internal `RunAsync` body. The whole pipeline is asynchronous end to end:
`Program.Main` returns `Task<int>`, `Program.RunAsync` awaits the command, and the dispatch table's handler is a
`Func<Context, Task>` (the synchronous commands are adapted with a completed task), so no sync-over-async
(`GetAwaiter().GetResult()`) exists in the commands.

### UninstallCommand

Awaits `catalog.UninstallAsync(modelId, ct)` and
reports success. `SpeechModelStore.Uninstall` is documented
as a safe no-op for a model with nothing installed, so this command follows that same contract —
"nothing to remove" is a successful outcome, not an error, since an idempotent cleanup script
should not fail merely because it ran twice. A missing `<modelId>` or an extra argument is an
`ArgumentException`. A genuine store failure
(`SpeechModelStoreException`, e.g. an open file handle preventing deletion) is caught and
re-thrown as `InvalidOperationException`, so `Program.Main`'s clean-error convention (a one-line
message, non-zero exit) applies instead of a stack trace, matching how the rest of the CLI
reports operator-facing failures. `Ctrl+C` is wired to a `CancellationTokenSource` through
`ConsoleCancellation.Register`; the token is observed only before the removal starts, and a
cancellation then is reported with `WriteError` and a non-zero exit code rather than propagating.

### CleanCommand

Awaits `catalog.CleanUpLeftoversAsync(modelId, ct)` and reports success. `SpeechModelStore.CleanUpLeftovers`
is documented as fully best-effort and never throwing for a failed deletion, so this handler needs no exception
handling of its own beyond cancellation (reported like `uninstall`'s, with `Ctrl+C` wired the same way); a model
with nothing to clean up is reported the same way as one with
leftovers actually removed. A missing `<modelId>` or an extra argument is an `ArgumentException`.

### ListVoicesCommand

Parses the optional `--format table|json` flag (anything else is an `ArgumentException`) and prints
`catalog.EnumerateVoices()` as an aligned table (Id, Source, Moods, Display Name) or an indented JSON
array. Moods is the number of emotion variants (`MoodClips`) the voice has, or `-`; each JSON row also
carries a `moods` array of the mood names. When there are no voices it explains how to obtain some (`voices install <packId>`,
the per-user folder `catalog.UserVoicesDirectory`, or `--voices-dir`). The table form also shows
how to choose a voice (`speak --tts-param voice=<id>`) and the reference-voice consent note
(`ReferenceVoiceLibrary.ConsentNotice`: only clone voices you have permission to use).

### VoicesCommand

Dispatches on the first argument:

- **`packs`** lists `catalog.VoicePacks` with each pack's `GetVoicePackState` (and, when installed, whether it
  has emotions), the download size, the installed size without and with emotions in MiB (from the
  pack's measured `VoicesBytes` / `EmotionsBytes`), and the consent note
- **`install <packId>`** calls `InstallVoicePackAsync` with an `IProgress` that prints throttled
  percent (or KB) lines, exactly as `DownloadCommand` does, and prints the consent note on success.
  Emotion variants are included by default; `--no-emotions` (a global option parsed into
  `Context.NoEmotions`) installs the base voices only. The command states which it is doing and, when
  the pack is installed without emotions and emotions are requested, announces that it will be removed
  and downloaded again. A failed or canceled install is reported with `WriteError` and a non-zero exit code. `Ctrl+C` is
  wired to a `CancellationTokenSource` the same way as `DownloadCommand`
- **`uninstall <packId>`** awaits `UninstallVoicePackAsync`, a safe no-op when nothing is installed; a
  `SpeechModelStoreException` is re-thrown as `InvalidOperationException`, and a `Ctrl+C` cancellation
  before the removal starts is reported with `WriteError` and a non-zero exit code
- **`packs`** reads each pack's `GetVoicePackState` once per invocation (the value is reused for the
  installed-with-emotions annotation)

An unknown pack id, a missing or extra argument, or an unknown action is an `ArgumentException`.
Because the catalog is built from the same `--models-dir` and `--mirror-*` options, pack downloads
honor a mirror (`{mirror}/<packId>/<packId>.zip`) without any command-specific logic.
Installed pack voices appear on the next invocation, since the voice list is fixed when the
catalog (and the Chatterbox model) is constructed.

### Chatterbox and Voice Seam Members

`SpeechModelCatalogAdapter` builds `.AddSherpaModels().AddKokoroModels().AddNemotronSttModels()`,
creates one `ReferenceVoiceLibrary` over the catalog's store and the mirror-aware downloader
options, and then calls `AddChatterboxTurboModels` with `ReferenceVoiceDirectory = --voices-dir`
and that same library, so the model and the voice commands see identical voices. `ICliModelCatalog`
gains `VoicePacks`, `UserVoicesDirectory`, `EnumerateVoices()`, `GetVoicePackState`,
`InstallVoicePackAsync(packId, includeEmotions, progress, ct)`, `VoicePackHasEmotionsAsync`, and
`UninstallVoicePackAsync`. Voice choice itself uses the model's existing
generic `voice` choice parameter (`speak --tts-param voice=<id>`). Voices from an explicit
`--voices-dir` are decoded lazily (`FromFileLazy`) like pack and user-folder voices: the file is read
only when that voice is selected, so an undecodable file does not fail the commands that build the
catalog.

### Interactions with Other Units

Every command depends only on `ICliModelCatalog` and on the library's descriptor, state,
parameter, progress, and result value types — none touches `SpeechModelCatalog` or
`SpeechModelStore` directly. This is what allows the full argument-parsing, filtering, error, and
progress-reporting behavior of all seven commands to be verified deterministically with no network
access and no real model download, using a fake `ICliModelCatalog`.

### Seam Extension for SynthesisCommandSubsystem and RecognitionCommandSubsystem

`speak` needs to load a real `ISpeechSynthesizerEngine`, and `recognize` needs to load a real
`ISpeechRecognizerEngine`, each of which requires casting a resolved model to the library's fully
public `ISynthesisModel`/`IRecognitionModel` interface - both interfaces are implementable (and
their `is`/`as` casts compilable) from any assembly, including `DemaConsulting.Speech.Cli.Tests`.
Rather than introduce competing seam interfaces for each subsystem, `ICliModelCatalog` is extended
with three further members that keep the same shape as the four model-management members - narrow,
purpose-specific operations, never throwing for a genuinely absent capability where the library
itself would return a graceful fallback - so the role-specific narrowing stays confined to one
seam instead of being repeated across every caller:

| Member | Returns | Behavior |
| --- | --- | --- |
| `GetPreferredAudioFormat(descriptor)` | `AudioFormat` | Throws for a non-synthesis model |
| `CreateSynthesizerEngineAsync(descriptor, values, ct)` | `Task<ISpeechSynthesizerEngine>` | Non-synthesis throws |
| `CreateRecognizerEngineAsync(descriptor, values, ct)` | `Task<ISpeechRecognizerEngine>` | Non-recognition throws |

`SpeechModelCatalogAdapter` implements all three through private `RequireSynthesisModel`/
`RequireRecognitionModel` helpers that perform the respective `is ISynthesisModel`/
`is IRecognitionModel` cast once, throwing a clean, model-id-naming `ArgumentException` when it
fails. `CreateSynthesizerEngineAsync`/`CreateRecognizerEngineAsync` forward the descriptor, the optional
resolved `parameterValues` bag, and the token unchanged to the
library's `SpeechSynthesizerFactory.LoadAsync`/`SpeechRecognizerFactory.LoadAsync` static methods,
each returning an engine with no device bound yet; the caller (`SpeakCommand`/`RecognizeCommand`/
`AskCommand`) binds the resolved playback/capture device afterward via
`engine.CreateSessionAsync(device, cancellationToken)`. See _SpeechCli SynthesisCommandSubsystem
Design_ and _SpeechCli RecognitionCommandSubsystem Design_ for how each command consumes its
members.
