## App

**Purpose**: The Avalonia application class and the demo's composition root. It builds the real
library catalog, wires the panel view models into the main window, and shuts everything down in
order when the window closes. Composition is a testable seam so it can be verified without a
desktop lifetime.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `LaunchOptions` | static `AppLaunchOptions?` | Options parsed by `Program`, read when composing |
| `_shuttingDown` | `bool` | Set when shutdown starts so a repeated close request is ignored |
| `ShutdownTask` | internal `Task?` | The in-flight asynchronous shutdown, retained so it is observed and testable |
| `CatalogFactory` | local function | `SpeechModelDownloaderOptions?` to `ModelCatalogComposition`; not a member |

**Key Methods**:

- `OnFrameworkInitializationCompleted` composes the desktop shell by calling `Compose` with
  `LaunchOptions` and assigns the result to the main window.
- `Compose(AppLaunchOptions?, out ModelCatalogService)` is the internal composition seam. It builds
  the audio device service, the model catalog composition and service, and the synthesis and
  recognition panels, and returns the `MainWindowViewModel`. It reads the supplied options rather
  than the static property, so tests compose with isolated options.
- `CatalogFactory` is a composition-root local function that maps the supplied
  `SpeechModelDownloaderOptions?` to a `ModelCatalogComposition`. It creates a `SpeechModelCatalog`
  from the launch store options and those downloader options, registers the Sherpa, Kokoro and
  Nemotron model families, builds one `ReferenceVoiceLibrary` over the catalog's store, registers
  the Chatterbox Turbo model with that shared library and the optional `--voices-dir` folder, and
  returns the catalog and library together as the composition. The factory builds its own
  `SpeechModelStore` for the voice library because the catalog keeps its store private.
- The shutdown handler defers application exit: it cancels the first shutdown request, sets
  `_shuttingDown`, and starts `CompleteShutdownAsync`, keeping the task in `ShutdownTask`.
- `CompleteShutdownAsync` calls `RunTeardownAsync` to dispose the synthesis panel, then the
  recognition panel, then the model catalog service, and finally calls the desktop lifetime's
  shutdown from a `finally` block so exit always completes.
- `RunTeardownAsync(params Func<ValueTask>[])` runs each step in order inside its own `try/catch`,
  so a failing step neither skips the remaining steps nor propagates.

**Error Handling**: Each disposal failure is contained separately and does not block the remaining
steps or application exit. A repeated shutdown request while shutdown is in progress is ignored.

**Dependencies**: `AppLaunchOptions`, `MirrorOptionsFactory` (indirectly, through the options),
the ShellSubsystem view models, the ModelCatalogSubsystem (`ModelCatalogComposition` and
`ModelCatalogService`), a separate `SpeechModelStore`, and the library's `SpeechModelCatalog`,
`SpeechModelDownloaderOptions`, `ReferenceVoiceLibrary` and model registration extensions.

**Callers**: `Program` (through `BuildAvaloniaApp`) and the Avalonia runtime.
