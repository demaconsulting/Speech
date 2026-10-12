## Program

**Purpose**: The process entry point. It parses and validates the command line, then starts the
Avalonia desktop application.

**Data Model**: Stateless static class. `BuildAvaloniaApp` returns the configured
`AppBuilder` for the demo `App`.

**Key Methods**:

- `Main` parses `AppLaunchOptions` from the arguments and validates the mirror settings eagerly by
  calling `CreateDownloaderOptions`. On `--help` it prints the option summary and exits. Otherwise
  it stores the options on `App.LaunchOptions` and starts the classic desktop lifetime.
- `BuildAvaloniaApp` configures Avalonia with the platform detection and logging the demo uses.

**Error Handling**: An `ArgumentException` from parsing or mirror validation is caught; `Main`
prints the message and the option summary and returns exit code 1, so a bad command line fails fast
before any window appears.

**Dependencies**: `AppLaunchOptions`, `App` and Avalonia.

**Callers**: The operating system process start.
