## AppLaunchOptions

**Purpose**: Parse the demo's command-line arguments into a typed options object and translate
them into the library's store and downloader options, so the same configuration is available at
launch as in the model catalog panel.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `ModelsDir` | `string?` | Overrides the model store root folder (`--models-dir`) |
| `VoicesDir` | `string?` | Optional folder of user reference voices (`--voices-dir`), which must exist |
| `MirrorUrl`, `MirrorUser`, `MirrorPassword`, `MirrorBearerToken` | `string?` | Mirror settings (`--mirror-*`) |
| `Help` | `bool` | Set by `-h`, `-?` or `--help` |

**Key Methods**:

- `Parse(string[])` reads the arguments left to right, each option taking the next argument as its
  value. `Parse(string[], Func<string, string?>)` additionally reads the `DEMASPEECH_MIRROR_URL`,
  `DEMASPEECH_MIRROR_USER`, `DEMASPEECH_MIRROR_PASSWORD` and `DEMASPEECH_MIRROR_BEARER_TOKEN`
  environment variables through an injectable reader (the one-argument overload passes
  `Environment.GetEnvironmentVariable`), so tests never mutate the process environment. A
  command-line `--mirror-url` overrides the URL variable; any credential option on the command line
  disables all credential variables so the two sources are never mixed; an empty variable is
  treated as unset. Secret values and rejected URL values are never written to help or error text.
  An empty option value (URL, user, password or token) is likewise treated as unset, so it neither
  blocks the environment fallback nor counts as a command-line credential. A repeated option keeps
  its last value. The `--voices-dir` folder is checked once, at parse time, unless help was
  requested; the models directory is not checked.
- `CreateStoreOptions()` returns store options with the root path override, or `null` when no
  models folder was given.
- `CreateDownloaderOptions()` delegates mirror validation and construction to
  `MirrorOptionsFactory` and returns `null` when no mirror was requested.
- `PrintHelp(TextWriter)` writes the option summary to the supplied writer (throwing
  `ArgumentNullException` for a null writer); `PrintHelp()` writes to `Console.Out`. The summary
  lists the help flags, `--mirror-bearer-token` and the four `DEMASPEECH_MIRROR_*` variables,
  exposed as the `MirrorUrlEnvironmentVariable`, `MirrorUserEnvironmentVariable`,
  `MirrorPasswordEnvironmentVariable` and `MirrorBearerTokenEnvironmentVariable` constants.

**Error Handling**: `Parse` throws `ArgumentNullException` for null arguments or a null
environment reader, and `ArgumentException` for an unrecognized argument, an option missing its
value, or a `--voices-dir` folder that does not exist. An unrecognized argument is reported by its
1-based position only, never by its text, because a mistyped option may be a secret.
`CreateDownloaderOptions` surfaces the
factory's `ArgumentException`.

**Dependencies**: `MirrorOptionsFactory` and the library's `SpeechModelStoreOptions` and
`SpeechModelDownloaderOptions`.

**Callers**: `Program` (parses and validates eagerly at startup) and `App` (reads the options when
composing the shell).
