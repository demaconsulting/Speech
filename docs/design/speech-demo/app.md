## App

**Purpose**: The Avalonia application class and the demo's composition root. It builds the real
library catalog, wires the panel view models into the main window, and shuts everything down in
order when the window closes. Composition is a testable seam so it can be verified without a
desktop lifetime.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `LaunchOptions` | static `AppLaunchOptions?` | Options parsed by `Program`, read when composing |
| `_shuttingDown` | `bool` | Set when shutdown starts so no second cleanup is started |
| `_cleanupCompleted` | `bool` | Set when cleanup has finished, just before shutdown resumes |
| `ShutdownTask` | internal `Task?` | The in-flight asynchronous shutdown, retained so it is observed and testable |
| `CatalogFactory` | local function | `SpeechModelDownloaderOptions?` to `ModelCatalogComposition`; not a member |

**Key Methods**:

- `OnFrameworkInitializationCompleted` composes the desktop shell by calling `Compose` with
  `LaunchOptions`, assigns the result to the main window and subscribes `ShutdownRequested`,
  delegating to `HandleShutdownRequested` and cancelling the request when it returns true.
- `HandleShutdownRequested(Action, MainWindowViewModel)` is the testable shutdown decision. The
  first call sets `_shuttingDown`, starts `CompleteShutdownAsync` and returns true (defer). A repeated
  call returns true while cleanup is pending, so shutdown can never proceed mid-cleanup, and false
  once `_cleanupCompleted` is set.
- `Compose(AppLaunchOptions?, out ModelCatalogService)` is the internal composition seam. It builds
  the audio device service, the model catalog composition and service, and the synthesis and
  recognition panels, and returns the `MainWindowViewModel`. It reads the supplied options rather
  than the static property, so tests compose with isolated options.
- `BuildOrDispose<T>(IDisposable, Func<T>)` runs the panel composition and disposes the supplied
  owner (the catalog service) before rethrowing when it fails; it is a seam so the guard is
  testable directly.
- `CatalogFactory` is a composition-root local function that maps the supplied
  `SpeechModelDownloaderOptions?` to a `ModelCatalogComposition`. It creates a `SpeechModelCatalog`
  from the launch store options and those downloader options, registers the Sherpa, Kokoro and
  Nemotron model families, builds one `ReferenceVoiceLibrary` over the catalog's store, registers
  the Chatterbox Turbo model with that shared library and the optional `--voices-dir` folder, and
  returns the catalog and library together as the composition. The factory builds its own
  `SpeechModelStore` for the voice library because the catalog keeps its store private.
- The shutdown handler defers application exit: it cancels the first shutdown request, sets
  `_shuttingDown`, and starts `CompleteShutdownAsync(Action, MainWindowViewModel)`, keeping the task
  in `ShutdownTask`.
- `CompleteShutdownAsync` calls `RunTeardownAsync` to dispose the synthesis panel, then the
  recognition panel, then the model catalog service, and finally sets `_cleanupCompleted` and calls
  the supplied resume-shutdown action from a `finally` block so exit always completes.
- `RunTeardownAsync(params Func<ValueTask>[])` runs each step in order inside its own `try/catch`,
  so a failing step neither skips the remaining steps nor propagates.

**Error Handling**: Each disposal failure is contained separately and does not block the remaining
steps or application exit. A repeated shutdown request while cleanup is pending is cancelled and
starts no second cleanup. If constructing the shell view model fails, `BuildOrDispose` (the seam
`Compose` builds the panels through) disposes the catalog service before rethrowing so nothing
leaks. `CatalogFactory` disposes the half-built catalog and voice library before rethrowing when
its own registration fails, so a failed composition or rebuild never leaves them undisposed.

**Dependencies**: `AppLaunchOptions`, `MirrorOptionsFactory` (indirectly, through the options),
the ShellSubsystem view models, the ModelCatalogSubsystem (`ModelCatalogComposition` and
`ModelCatalogService`), a separate `SpeechModelStore`, and the library's `SpeechModelCatalog`,
`SpeechModelDownloaderOptions`, `ReferenceVoiceLibrary` and model registration extensions.

**Callers**: `Program` (through `BuildAvaloniaApp`) and the Avalonia runtime.
