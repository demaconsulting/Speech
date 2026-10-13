## Program

**Purpose**: The process entry point of `speech-cli`. It turns the command line into a `Context`, runs
the highest-priority top-level action (version, help, self-validation, or the selected subcommand),
and converts operator-facing failures into a one-line error and a non-zero exit code.

**Data Model**: Stateless static class with no fields.

| Member | Purpose |
| --- | --- |
| `Version` | Informational version, else assembly version, else `0.0.0`; read on every access |
| `Main(args)` | The process entry point; returns the process exit code |
| `RunAsync(context)` | Runs the selected action for an already-built `Context` |

**Key Methods**:

- `Main` builds the `Context` with `Context.Create`, awaits `RunAsync`, and returns the context's
  exit code. The context is held in a variable declared outside the `try` so the `catch` blocks can
  consult `Context.Silent` when `Context.Create` succeeded but a later step failed. The context is
  disposed (closing any `--log` file) when `Main` leaves the `try`.
- `RunAsync` rejects a null context with `ArgumentNullException` (surfaced through the returned
  task), then performs exactly one action in priority order:
  1. `--version`: writes only the version string and returns, before the banner is printed
  2. otherwise prints the banner (version line, copyright line, blank line), then for `--help`
     prints the usage text and returns
  3. `--validate`: runs `Validation.RunAsync` and returns
  4. otherwise dispatches the subcommand
- The usage text contains, in order: the usage line, every global option,
  the `DEMASPEECH_MIRROR_*` environment variable names (names only, never values), the
  usage summary of every `CommandDispatch` entry in table order, a pointer to `doctor`, and the
  voice-cloning hint with the reference-voice consent notice. Because the command list is generated
  from `CommandDispatch.Commands`, help and dispatch cannot disagree.
- Subcommand dispatch with no resolved subcommand prints the same usage text as `--help` (the
  banner has already been printed) and succeeds. Otherwise it looks the name up with
  `CommandDispatch.Find` and awaits the descriptor's handler. `Context.Create` has already rejected
  unknown names, so a failed lookup is an internal error reported as `InvalidOperationException`.
- `IsSilentRequested` decides whether console error output is suppressed: the constructed context's
  `Silent` value when a context exists, otherwise a literal scan of the raw arguments for
  `--silent` (needed when `Context.Create` itself threw, for example for an unknown subcommand).

**Error Handling**: `Main` maps exceptions as follows, and every message write honors `--silent`
(the exit code and the rethrow never do):

| Exception | Output | Result |
| --- | --- | --- |
| `ArgumentException` | `Error: {message}` on the error stream | Returns `1` |
| `InvalidOperationException` | `Error: {message}` on the error stream | Returns `1` |
| Any other exception | `Unexpected error: {message}` on the error stream | Rethrown for crash logs |

No stack trace is printed for the expected exception types. A command that reports failure through
`Context.WriteError` instead of throwing produces `Context.ExitCode` `1` from the normal return path.
`Version` never throws; a missing informational version falls back as described above.

**Dependencies**: `Context` (argument parsing and output), `CommandDispatch` (subcommand lookup and
help text), `Validation` (the `--validate` action), and the library's `ReferenceVoiceLibrary` for the
consent notice shown in the usage text.

**Callers**: The operating system process start calls `Main`. `Validation` calls `Version` and
`RunAsync` (its version and help self-tests run the real program logic against an inner context).
