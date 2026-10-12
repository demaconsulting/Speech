## App

**Purpose**: The Avalonia application class and composition root. It builds the real library
catalog, wires the panel view models into the main window, and shuts everything down in order when
the window closes.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `LaunchOptions` | static `AppLaunchOptions?` | Options parsed by `Program`, read when the shell is composed |
| `CatalogFactory` | local function | `SpeechModelDownloaderOptions?` to `ModelCatalogComposition`; not a member |

**Key Methods**:

- `OnFrameworkInitializationCompleted` composes the desktop shell: the audio device service, the
  model catalog composition and service, and the synthesis and recognition panels, and assigns the
  result to the main window.
- `CatalogFactory` is a composition-root local function that maps the supplied
  `SpeechModelDownloaderOptions?` to a `ModelCatalogComposition`. It creates a `SpeechModelCatalog`
  from the launch store options and those downloader options, registers the Sherpa, Kokoro and
  Nemotron model families, builds one `ReferenceVoiceLibrary` over the catalog's store, registers
  the Chatterbox Turbo model with that shared library and the optional `--voices-dir` folder, and
  returns the catalog and library together as the composition.
- The shutdown handler defers application exit: it cancels the first shutdown request, marks the
  application as shutting down, and completes shutdown asynchronously by disposing the synthesis
  panel, then the recognition panel, then the model catalog service, and finally calling the
  desktop lifetime's shutdown. Each disposal failure is contained so that exit always completes.

**Error Handling**: Failures while disposing panels or the catalog service are caught and do not
block application exit. A repeated shutdown request while shutdown is in progress is ignored.

**Dependencies**: `AppLaunchOptions`, `MirrorOptionsFactory` (indirectly, through the options),
the ShellSubsystem view models, the ModelCatalogSubsystem (`ModelCatalogComposition` and
`ModelCatalogService`), a separate `SpeechModelStore`, and the library's `SpeechModelCatalog`,
`SpeechModelDownloaderOptions`, `ReferenceVoiceLibrary` and model registration extensions.

**Callers**: `Program` (through `BuildAvaloniaApp`) and the Avalonia runtime.
