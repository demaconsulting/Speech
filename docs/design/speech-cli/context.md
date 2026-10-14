## Context

**Purpose**: Parse the command line into the program's configuration, resolve the download mirror,
and own the program's output (console and optional log file) and proposed exit code. A single
`Context` carries every parsed option, the resolved subcommand name and its raw arguments from
`Program` to the subcommand handlers.

**Data Model**: Sealed, disposable class created only through `Context.Create`; every property is
set once during creation.

| Member | Type | Purpose |
| --- | --- | --- |
| `Version`, `Help`, `Validate` | `bool` | Top-level action flags (`-v`/`--version`, `-?`/`-h`/`--help`, `--validate`) |
| `Silent` | `bool` | Suppresses console output (`--silent`) |
| `Diagnostics` | `bool` | Verbose output requested (`--verbose` or `--diagnostics`) |
| `NoEmotions` | `bool` | `--no-emotions`; only `voices install` acts on it |
| `ModelsDir` | `string?` | `--models-dir` store-root override; `null` selects the per-user default |
| `VoicesDir` | `string?` | `--voices-dir` extra reference-voice folder; when set it exists |
| `MirrorUrl`, `MirrorUser`, `MirrorPassword`, `MirrorBearerToken` | `string?` | Mirror options or variables |
| `ResultsFile` | `string?` | `--results` (alias `--result`) results file for `--validate` |
| `HeadingDepth` | `int` | `--depth`, 1 to 6, default 1 |
| `Command` | `string?` | The resolved subcommand name, or `null` when none was given |
| `CommandArgs` | `IReadOnlyList<string>` | Arguments after the subcommand, minus global options |
| `ExitCode` | `int` | `1` once `WriteError` has been called, otherwise `0` |

The four environment variable names are public constants
(`MirrorUrlEnvironmentVariable`, `MirrorUserEnvironmentVariable`,
`MirrorPasswordEnvironmentVariable`, `MirrorBearerTokenEnvironmentVariable`). The log writer is a
private field; an internal `ArgumentParser` class holds the parse state and is discarded after
creation.

**Key Methods**:

- `Create(args)` and `Create(args, getEnvironmentVariable)`: the factory. The second form takes the
  environment reader as an injectable function (the first passes
  `Environment.GetEnvironmentVariable`), so tests never touch the process environment. Creation runs
  in a fixed order, so the first failing step determines the reported error:
  1. Parse the arguments left to right.
  2. Merge the mirror environment variables (see below).
  3. Call `CreateMirror` once, so a bad mirror combination is rejected for every invocation,
     including `--help` and `--version`.
  4. Reject a `--voices-dir` that is not an existing directory.
  5. Open the `--log` file last, so no file is created when an earlier step fails.
- Parsing: every global option is recognized at any position relative to the subcommand. The first
  argument that is not a global option must name a recognized subcommand (checked with
  `CommandDispatch.Find`) and becomes `Command`; every later argument that is not a global option is
  appended, in order, to `CommandArgs` and is interpreted by the subcommand's own handler. A global
  option that takes a value (`--models-dir`, `--voices-dir`, `--mirror-*`, `--log`, `--results`,
  `--depth`) consumes the next token unconditionally, even when that token itself looks like an
  option.
- Global tokens are absorbed wherever they appear: a subcommand value that equals a global token,
  for example `speak --text -v`, is consumed as the global option (here `--version`) and never
  reaches `CommandArgs`. A value that must look like a global token cannot be supplied to a
  subcommand. No test exercises this behavior directly; the placement tests cover a global option
  following the subcommand.
- `--depth` accepts only an integer from 1 to 6.
- Mirror environment merge: an explicit `--mirror-url` wins over `DEMASPEECH_MIRROR_URL`. The three
  credential variables apply only when none of `--mirror-user`, `--mirror-password` and
  `--mirror-bearer-token` was given on the command line, so a command-line credential is never mixed
  with an environment one. An empty variable counts as unset.
- `CreateMirror()` returns `null` when no mirror URL is configured and otherwise a `DownloadMirror`.
  It rejects, as `ArgumentException`, a credential without a mirror URL, a mirror URL that is not an
  absolute URI, and a user without a password (or the reverse). It forwards everything else, the
  scheme, query, fragment and user-info shape, Basic-plus-bearer exclusivity, a blank token, and
  cleartext `http` with credentials to a non-loopback host, to `DownloadMirror`'s own constructor,
  so those rules live in one place.
- `WriteLine(message)` writes to standard output unless `Silent`, and to the log file when one is open.
- `WriteError(message)` first marks the context as having errors, so `ExitCode` is `1` even when
  `Silent` suppresses the console. Unless `Silent`, it writes to the error stream in red and
  restores the previous foreground color in a `finally` block, so the color is restored even when
  the error write throws. The log write follows the console write, so when the console write throws
  the exception propagates and that message is not logged.
- `Dispose()` closes the log writer; it is safe to call more than once.

**Error Handling**: Argument problems are `ArgumentException` and a log file that cannot be opened is
`InvalidOperationException` wrapping the underlying exception; `Program.Main` reports both as a
one-line error without a stack trace.

- The exceptions are constructed without a parameter name, so messages carry no
  `(Parameter '...')` suffix.
- An unrecognized first non-global token fails with `Unsupported argument '{token}'`, which names the
  token (this includes an unknown option such as `--unknown` placed before any subcommand; the same
  token after a subcommand is collected into `CommandArgs` instead).
- A value option with no following token fails with `{option} requires {description}`.
- A bad `--depth` fails with a message naming the option and the 1 to 6 range.
- A bad `--voices-dir` fails with a message naming the option and the path.
- A bad mirror URL is rejected with a message that does not echo the value, and no mirror credential
  or environment value is ever written to a message, because a mistyped value can carry a pasted
  secret.
- Output is not synchronized; a `Context` is used from the single command flow and is not
  thread-safe.

**Dependencies**: `CommandDispatch` (subcommand-name validation) and the library's `DownloadMirror`
(mirror construction and its validation rules).

**Callers**: `Program` (creation, banner, usage, exit code), `Validation` (inner contexts for its
version and help self-tests, `ModelsDir`, `HeadingDepth`, `ResultsFile`), and every subcommand
handler (`CommandArgs`, `ModelsDir`, `VoicesDir`, `CreateMirror`, `NoEmotions`, `Diagnostics`,
`WriteLine`, `WriteError`).
