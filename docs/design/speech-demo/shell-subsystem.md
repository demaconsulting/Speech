## SpeechDemo ShellSubsystem Design

![ShellSubsystem Structure](ShellSubsystemView.svg)

### Overview

The ShellSubsystem owns the application's start-up composition and its navigation surface. It
contains the following units:

- **MainWindowViewModel**: the window's navigation state — the ordered list of panels, the
  currently selected panel, and the window title
- **DemoPanelViewModel**: one navigable entry — a display title paired with the panel view
  model the shell was composed with

The composition root itself lives in the `App` unit (`App.axaml.cs`) alongside the process entry
point, the `Program` unit (`Program.cs`). Both, together with `AppLaunchOptions` and
`MirrorOptionsFactory`, are units directly under the SpeechDemo system rather than members of
this subsystem; see their own design documents.

### Interfaces

The subsystem exposes `MainWindowViewModel` (the main window's data context) and
`DemoPanelViewModel` to the `App` composition root and to `MainWindow.axaml`. It consumes the
four panel view models, `DeviceSelectionViewModel`, `ModelCatalogViewModel`,
`SynthesisPanelViewModel` and `RecognitionPanelViewModel`, through their concrete types only.

### Design

The subsystem is a thin navigation surface: `MainWindowViewModel` wraps each injected panel view
model in a titled `DemoPanelViewModel`, in a fixed order, and tracks the selected panel. The
composition performed by the `App` unit is summarized next for context, then each unit is
described.

### Composition Root

`App.OnFrameworkInitializationCompleted` (see _SpeechDemo App Design_) is the single place the
demo's object graph is built:

1. Construct the library's `AudioDeviceFactory`
2. Resolve the store and downloader options from `AppLaunchOptions` (when present)
3. Define a `CatalogFactory` that builds a `ModelCatalogComposition`: a `SpeechModelCatalog`
   (populated through `AddSherpaModels()`, `AddKokoroModels()` and `AddNemotronSttModels()`)
   paired with one shared `ReferenceVoiceLibrary` over the catalog's store, then registers the
   Chatterbox Turbo model through `AddChatterboxTurboModels()` over that same library (and the
   optional `--voices-dir` folder), since core `DemaConsulting.Speech` ships zero built-in models
4. Construct a separate `SpeechModelStore` over the same store options, used by the session
   factories to locate an installed model's files
5. Wrap each in its demo-owned adapter/seam (`AudioDeviceService`, `ModelCatalogService` built
   over the initial composition, the downloader options and `CatalogFactory`,
   `SynthesizerSessionFactory`, `RecognizerSessionFactory`)
6. Construct `DeviceSelectionViewModel`, `ModelCatalogViewModel` (seeded with the launch-time
   mirror strings), `SynthesisPanelViewModel` and `RecognitionPanelViewModel` (the last two
   share the one `DeviceSelectionViewModel`) over those adapters
7. Construct `MainWindowViewModel` over the four panel view models and assign it as the main
   window's data context
8. On shutdown, defer the shutdown, asynchronously dispose the `SynthesisPanelViewModel` and the
   `RecognitionPanelViewModel` (each in turn awaits the library's own async engine/session
   teardown), then dispose the catalog service, and finally resume the shutdown

Disposing the two session-owning panels before the catalog matters because a live session
holds a lease against the engine the catalog's `SpeechModelCatalog`/`SpeechModelStore` loaded;
tearing the panels down first lets each session release its engine's lease cleanly before the
catalog that produced it goes away. `desktop.ShutdownRequested` has no async-aware overload, so
the handler cancels the pending shutdown, runs the cleanup to genuine completion, and then
calls `Shutdown()` again; it does not use an `async void` handler.

No dependency-injection container is used. The demo's purpose is to show a reader exactly how a
host application wires itself to the library, and a container would move that wiring into
configuration a reader has to reverse-engineer. The cost — a constructor call per collaborator —
is trivial at this size.

### MainWindowViewModel

`MainWindowViewModel` is constructed with the four panel view models and rejects a null for
any of them, because the demo composes manually and the constructor is the only place a missing
collaborator can be detected before a half-composed window reaches a user.

| Member | Type | Purpose |
| --- | --- | --- |
| `Title` | `static string` | Window title naming the four panels |
| `Panels` | `ObservableCollection<DemoPanelViewModel>` | The ordered navigable panels |
| `SelectedPanel` | `DemoPanelViewModel?` | The panel currently shown |
| `DeviceSelection` | `DeviceSelectionViewModel` | The injected device panel state |
| `ModelCatalog` | `ModelCatalogViewModel` | The injected catalog panel state |
| `Synthesis` | `SynthesisPanelViewModel` | The injected text-to-speech panel state |
| `Recognition` | `RecognitionPanelViewModel` | The injected speech-to-text panel state |

`Panels` is built once, during construction, by wrapping each injected panel view model in a
titled `DemoPanelViewModel` entry, in the fixed order Audio Devices, Model Catalog, Text to Speech,
Speech to Text; the shell never constructs or replaces the panel state itself. `SelectedPanel`
is initialized to the first entry so the window never opens empty, and raises change
notification on every assignment so the bound view can swap panels with no code-behind.

The window title names the four panels the shell presents.

### DemoPanelViewModel

`DemoPanelViewModel` pairs a display `Title` with the `Content` view model to show. It rejects a
null or blank title, and a null content, because a navigation entry with no title or no content
is unusable rather than merely degraded.

Keeping the entry as a distinct type — rather than navigating over the panel view models
directly — is what lets the shell present a stable, titled navigation list without every panel
view model having to know it is being navigated to.

### Interactions with Other Units

The shell depends on the four panel view models only through their concrete types, and on
nothing else. It never touches the library, the demo's service seams, or Avalonia; that
separation is what allows the whole navigation surface to be unit tested with no UI toolkit and
no audio hardware.
