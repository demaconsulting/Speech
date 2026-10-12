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
  value.
- `CreateStoreOptions()` returns store options with the root path override, or `null` when no
  models folder was given.
- `CreateDownloaderOptions()` delegates mirror validation and construction to
  `MirrorOptionsFactory` and returns `null` when no mirror was requested.
- `PrintHelp` writes the option summary to a text writer.

**Error Handling**: `Parse` throws `ArgumentNullException` for null arguments and
`ArgumentException` for an unrecognized argument, an option missing its value, or a `--voices-dir`
folder that does not exist. `CreateDownloaderOptions` surfaces the factory's `ArgumentException`.

**Dependencies**: `MirrorOptionsFactory` and the library's `SpeechModelStoreOptions` and
`DownloaderOptions`.

**Callers**: `Program` (parses and validates eagerly at startup) and `App` (reads the options when
composing the shell).
