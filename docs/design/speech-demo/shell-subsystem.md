## SpeechDemo ShellSubsystem Design

![ShellSubsystem Structure](ShellSubsystemView.svg)

### Overview

The ShellSubsystem owns the application's start-up composition and its navigation surface. It
contains the following units:

- **MainWindowViewModel**: the window's navigation state — the ordered list of panels, the
  currently selected panel, and the window title
- **DemoPanelViewModel**: one navigable entry — a display title paired with the panel view
  model the shell was composed with

The composition root itself lives in the application class (`App.axaml.cs`) alongside the
process entry point (`Program.cs`), because Avalonia owns application start-up and requires
both to sit at the application's root namespace.

### Composition Root

`App.OnFrameworkInitializationCompleted` is the single place the demo's object graph is built:

1. Construct the library's `AudioDeviceFactory`
2. Construct the library's `SpeechModelCatalog` and populate it via the sibling
   `DemaConsulting.Speech.Sherpa` package's `AddSherpaModels()` extension method
   (`new SpeechModelCatalog().AddSherpaModels()`), since core `DemaConsulting.Speech` ships zero
   built-in models
3. Construct a `SpeechModelStore` over the same options as the catalog
4. Wrap each in its demo-owned adapter/seam (`AudioDeviceService`, `ModelCatalogService`,
   `SynthesizerSessionFactory`, `RecognizerSessionFactory`)
5. Construct `DeviceSelectionViewModel`, `ModelCatalogViewModel`, `SynthesisPanelViewModel`, and
   `RecognitionPanelViewModel` over those adapters
6. Construct `MainWindowViewModel` over the four panel view models and assign it as the main
   window's data context
7. On shutdown, asynchronously dispose the `SynthesisPanelViewModel` and
   `RecognitionPanelViewModel` (each in turn awaits the library's own async engine/session
   teardown), then dispose the catalog

Disposing the two session-owning panels before the catalog matters because a live session
holds a lease against the engine the catalog's `SpeechModelCatalog`/`SpeechModelStore` loaded;
tearing the panels down first lets each session release its engine's lease cleanly before the
catalog that produced it goes away. `desktop.ShutdownRequested` has no async-aware overload, so
this handler is itself `async void`-shaped (fire-and-forget), the same accepted pattern used
throughout this demo's ViewModels for handlers whose signature cannot be `async Task`.

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
| `Title` | `static string` | Window title naming the capabilities this build implements |
| `Panels` | `ObservableCollection<DemoPanelViewModel>` | The ordered navigable panels |
| `SelectedPanel` | `DemoPanelViewModel?` | The panel currently shown |
| `DeviceSelection` | `DeviceSelectionViewModel` | The injected device panel state |
| `ModelCatalog` | `ModelCatalogViewModel` | The injected catalog panel state |
| `Synthesis` | `SynthesisPanelViewModel` | The injected text-to-speech panel state |
| `Recognition` | `RecognitionPanelViewModel` | The injected speech-to-text panel state |

`Panels` is built once, during construction, by wrapping each injected panel view model in a
titled `DemoPanelViewModel` entry, in the fixed order Devices, Model Catalog, Text to Speech,
Speech to Text; the shell never constructs or replaces the panel state itself. `SelectedPanel`
is initialized to the first entry so the window never opens empty, and raises change
notification on every assignment so the bound view can swap panels with no code-behind.

The window title names only the capabilities the current build actually implements.

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
