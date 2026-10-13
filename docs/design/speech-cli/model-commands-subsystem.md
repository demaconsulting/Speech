## SpeechCli ModelCommandsSubsystem Design

![ModelCommandsSubsystem Structure](ModelCommandsSubsystemView.svg)

### Overview

The ModelCommandsSubsystem implements the five model-management subcommands dispatched to by
`CommandDispatch` (`list-models`, `model-info`, `download`, `uninstall`, and `clean`) and the two
reference-voice subcommands `list-voices` and `voices`. It contains one command handler per
subcommand, plus the shared catalog seam used by all of them. Each unit has its own design document
in the `model-commands-subsystem/` folder:

- **`ICliModelCatalog`** / **`SpeechModelCatalogAdapter`**: the CLI-owned seam over the library's
  `SpeechModelCatalog`/`SpeechModelStore`, and its real implementation
- **`CliModelCatalogFactory`**: composes a `SpeechModelCatalogAdapter` honoring `--models-dir`,
  `--voices-dir` and `--mirror-*`
- **`DownloadProgressReporter`**: the progress reporter shared by `download` and `voices install`
- **`ListModelsCommand`**, **`ModelInfoCommand`**, **`DownloadCommand`**, **`UninstallCommand`**,
  **`CleanCommand`**: one handler per model-management subcommand
- **`ListVoicesCommand`**, **`VoicesCommand`**: the reference-voice handlers

### Interfaces

**Exposed**: each command exposes a static entry point invoked from the `CommandDispatch` table:
`Run(Context)` for the synchronous commands (`list-models`, `model-info`, `list-voices`) and
`RunAsync(Context)` for `DownloadCommand`, `UninstallCommand`, `CleanCommand` and `VoicesCommand`, which await
their asynchronous work. `ICliModelCatalog` is the subsystem's public-to-the-CLI seam, and
`CliModelCatalogFactory.Create(context)` composes the real implementation. Errors for operator mistakes are
`ArgumentException` or `InvalidOperationException`, which `Program.Main` reports as a one-line message and
exit code `1`.

**Consumed**: the library's `SpeechModelCatalog`, `SpeechModelStore`, `SpeechModelDownloaderOptions`,
`SpeechModelDescriptor`, parameter, progress and result types, `ReferenceVoiceLibrary`,
`SpeechSynthesizerFactory`, `SpeechRecognizerFactory`, and the `AddSherpaModels`/`AddKokoroModels`/
`AddNemotronSttModels`/`AddChatterboxTurboModels` registration extensions; and `Context` for
global options (`--models-dir`, `--voices-dir`, `--mirror-*`, `--no-emotions`).

The `ICliModelCatalog` members fall into four groups:

| Group | Members |
| --- | --- |
| Model management | `Enumerate`, `DownloadAsync`, `UninstallAsync`, `CleanUpLeftoversAsync` |
| Format and engine | `GetPreferredAudioFormat`, `CreateSynthesizerEngineAsync`, `CreateRecognizerEngineAsync` |
| Reference voices: browse | `VoicePacks`, `UserVoicesDirectory`, `EnumerateVoices`, `GetVoicePackState` |
| Reference voices: manage | `VoicePackHasEmotionsAsync`, `InstallVoicePackAsync`, `UninstallVoicePackAsync` |

### Design

Each command is a static class with a public entry point that takes only the `Context`, creates the real
catalog through `CliModelCatalogFactory`, and delegates to an overload that takes the catalog. The overload
holds all parsing, filtering and formatting, so the whole subsystem can be verified with a fake catalog and no
network or model download.

**Catalog composition**: `CliModelCatalogFactory` builds the storage options (`--models-dir` becomes the store
root override) and the downloader options (the mirror from `--mirror-*`), and passes them, with `--voices-dir`,
to `SpeechModelCatalogAdapter`. The adapter builds one `SpeechModelCatalog` and registers the model packages
with `AddSherpaModels().AddKokoroModels().AddNemotronSttModels()`, because core `DemaConsulting.Speech` ships
no built-in models. It then builds a `ReferenceVoiceLibrary` over the catalog's store and the same downloader
options and registers the voice-cloning model with `AddChatterboxTurboModels`, handing it that library and the
`--voices-dir` folder. The model and the voice commands therefore share one store, one mirror and one set of
voices. Uninstall and cleanup go through `Store.UninstallAsync` and `Store.CleanUpLeftoversAsync`, and a
failure part-way through composition disposes what was already built. The adapter's design document covers the
details.

### Why a CLI-Owned Seam

The library's `SpeechModelCatalog` and `SpeechModelStore` are sealed, and `SpeechModelCatalog`'s test-friendly
constructor is `internal` to the library, so no CLI test could construct one over controlled data. Making that
constructor public, or granting the CLI test assembly access, would widen the library's surface to suit one
consumer. Instead the CLI owns `ICliModelCatalog`, a narrow interface over exactly the operations the commands
need, mirroring the demo application's `IModelCatalogService`. Every command depends only on `ICliModelCatalog`
and the library's descriptor, state, parameter, progress and result value types; none touches
`SpeechModelCatalog` or `SpeechModelStore` directly.

### Asynchronous execution

The seam's uninstall, cleanup and pack-query members are asynchronous (`Task`, with a `CancellationToken`);
`UninstallCommand`, `CleanCommand`, `DownloadCommand` and `VoicesCommand` await them in their catalog-taking
`RunAsync` overloads. The public `RunAsync(Context)` entry of each of these commands wires `Ctrl+C` to a
`CancellationTokenSource` through `ConsoleCancellation.Register` and awaits that overload. The whole pipeline is
asynchronous end to end: `Program.Main` returns `Task<int>`, `Program.RunAsync` awaits the command, and the
dispatch table's handler is a `Func<Context, Task>` (the synchronous commands are adapted with a completed
task), so no sync-over-async (`GetAwaiter().GetResult()`) exists in the commands.

### Seam Extension for SynthesisCommandSubsystem and RecognitionCommandSubsystem

`speak` needs a real `ISpeechSynthesizerEngine` and `recognize` needs a real `ISpeechRecognizerEngine`, each of
which requires a resolved model to be treated as the library's public `ISynthesisModel` or `IRecognitionModel`.
Rather than introduce a competing seam for each subsystem, `ICliModelCatalog` carries the format and engine
group of members (`GetPreferredAudioFormat`, `CreateSynthesizerEngineAsync`, `CreateRecognizerEngineAsync`),
with the same shape as the model-management members: narrow, purpose-specific operations. The role-specific
narrowing therefore stays confined to one seam instead of being repeated across every caller.

`SpeechModelCatalogAdapter` implements them through private `RequireSynthesisModel` and
`RequireRecognitionModel` helpers that perform the role check once and refuse a model of the wrong role with an
`ArgumentException` that names the model id. The engine members forward the descriptor's model, the catalog,
the optional resolved parameter values and the token to the library's
`SpeechSynthesizerFactory.LoadAsync` or `SpeechRecognizerFactory.LoadAsync`, each returning an engine with no
device bound yet; the caller (`SpeakCommand`, `RecognizeCommand` or `AskCommand`) binds the resolved device
afterwards through `engine.CreateSessionAsync(device, cancellationToken)`. The catalog only refuses a
wrong-role model; the operator-facing message that suggests `list-models --role tts|stt` belongs to the
commands. See _SpeechCli SynthesisCommandSubsystem Design_ and _SpeechCli RecognitionCommandSubsystem Design_
for how each command consumes these members.

### Interactions with Other Units

- `CommandDispatch` invokes the entry point of each of the seven commands.
- `Context` supplies `--models-dir`, `--voices-dir`, `--mirror-*` and `--no-emotions`, and receives all output
  and the exit code; `Context.CreateMirror` validates the mirror options before any download starts.
- `ConsoleCancellation` supplies the `Ctrl+C` token for `download`, `uninstall`, `clean` and `voices`.
- `CliModelCatalogFactory` and `ICliModelCatalog` are also consumed by `SpeakCommand`, `RecognizeCommand`,
  `AskCommand` and `DoctorCommand`.
- The library supplies the model catalog, store, downloader, reference-voice library and engine factories.
