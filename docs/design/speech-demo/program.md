## Program

**Purpose**: The process entry point. It parses and validates the command line, then starts the
Avalonia desktop application.

**Data Model**: Stateless static class. `BuildAvaloniaApp` returns the configured
`AppBuilder` for the demo `App`.

**Key Methods**:

- `Main` calls `Run` with the console writers, `Environment.GetEnvironmentVariable` and an action
  that stores the options on `App.LaunchOptions` and starts the classic desktop lifetime. It sets
  `Environment.ExitCode` only when `Run` returns non-zero.
- `Run(args, output, error, env, start)` is the internal, testable seam. It parses
  `AppLaunchOptions` using the supplied environment reader and validates the mirror settings
  eagerly by calling `CreateDownloaderOptions`. On `--help` it prints the option summary to the
  output writer and returns 0 without calling `start`. Otherwise it calls `start` with the parsed
  options and returns 0.
- `BuildAvaloniaApp` configures Avalonia with the platform detection and the Inter font and
  logging the demo uses.

**Error Handling**: An `ArgumentException` from parsing or mirror validation is caught; `Run`
writes the message and the option summary to the error writer and returns exit code 1 without
calling `start`, so a bad command line fails fast before any window appears.

**Dependencies**: `AppLaunchOptions`, `App` and Avalonia (platform detection and Inter font
packages).

**Callers**: The operating system process start.
