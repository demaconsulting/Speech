### DoctorCommand

**Purpose**: Implement the `doctor` subcommand: a broader environment and health check than the
CI-safe `--validate` self-tests, reported as a pass/fail checklist covering native runtimes, the
model store, audio devices and installed models.

**Data Model**: The unit is a static class with no state. Each check prints lines tagged `[ OK ]`,
`[INFO]` or `[FAIL]`, and the overall summary line reads `HEALTHY` or `UNHEALTHY`. The only value
carried between checks is the model-store writability flag, which alone decides the summary and the
exit code.

**Key Methods**:

- `Run(Context)`: the dispatch entry point; composes a real `AudioDeviceFactory`, a catalog through
  `CliModelCatalogFactory` honoring `--models-dir` and `--mirror-*`, and the resolved model-store
  root path, and calls the overload below.
- `Run(Context, IAudioCaptureDeviceProbe, IAudioPlaybackDeviceProbe, ICliModelCatalog, string)`: the
  testable overload. It prints the four sections in order and then the overall summary.
- `CheckNativeRuntimes`: reports whether PortAudio is resolvable, inferred from whether the resolved
  probes are the library's `Unavailable*` fallback types (a public proxy signal, since the library's
  own PortAudio initialization status is internal), and whether the native `sherpa-onnx-c-api`
  shared library loads.
- `CheckModelStore`: prints the root path, probes writability and prints the available disk space.
  The probe creates the root directory when it does not exist, writes an empty temporary marker
  file and deletes it. Creating the missing directory is a deliberate side effect, because
  writability cannot be probed otherwise.
- `CheckAudioDevices`: prints the input and output device counts only; a full enumeration is the
  job of `list-devices`.
- `CheckModels`: prints installed-versus-known model counts through the `ICliModelCatalog` seam.
- `ResolveSherpaOnnxNativeLibraryPath`, `ResolveSherpaOnnxNativeLibraryFileName` and
  `ResolveRuntimeIdentifier`: resolve the platform's runtime identifier (for example `win-x64`) and
  the `runtimes/<rid>/native/<file>` path, parameterized by the base directory so tests can use a
  fixture directory.

`[FAIL]` lines and the `UNHEALTHY` summary go to stderr through `WriteError`; every other line goes
to stdout. The summary is `HEALTHY` and the exit code zero whenever the model store is writable.

**SherpaOnnx resolvability scope.** There is no public, model-independent way to probe SherpaOnnx
from the library: constructing a recognizer or synthesizer needs an installed model and performs a
real native load. `doctor` therefore probes the native shared library directly. A bare
`NativeLibrary.TryLoad("sherpa-onnx-c-api", ...)` under-reports availability for a
framework-dependent build, because NuGet copies the platform-specific asset to
`runtimes/<rid>/native/` under the application base directory and a bare-name probe never searches
that nested folder (ordinary P/Invoke resolution does, through the application's dependency
manifest). `doctor` therefore first loads the concrete `runtimes/<rid>/native/<file>` path when it
exists, and otherwise falls back to the bare-name probe, which covers self-contained and
single-file publishes where native assets are flattened into the base directory. A successful load
is freed immediately so the module is not kept mapped. The check only proves that the native binary
for the current platform is present and loadable; it does not prove a recognizer or synthesizer can
be constructed or run inference. It is a necessary-but-not-sufficient signal and is documented as
such in the class remarks. Any exception during the probe is reported as "could not be loaded".

**Hard failure versus informational note.** Only the model store's writability affects the exit
code. The PortAudio and SherpaOnnx runtime checks and the audio device counts are informational
(`[INFO]`) and never cause a non-zero exit, for two reasons:

- This mirrors the library's graceful-degradation philosophy for optional hardware and native
  inference runtimes. Missing audio hardware or an unresolvable native library does not prevent the
  rest of the tool from working, and every command that needs one of them (`devices test`, `speak`,
  `recognize` and `ask`) already reports its own clean, specific error at the point of use, so
  reporting the same condition as a hard failure would be redundant and would make `doctor` fail on
  perfectly normal headless or CI machines.
- The model store is infrastructure the tool itself owns and needs for every model-management
  command. Its writability depends only on file-system permissions, not on optional hardware or a
  native library, so a `doctor` run that cannot even manage models is genuinely unhealthy.

**Error Handling**: A null `Context`, probe or catalog throws `ArgumentNullException`; a null or
empty model-store root path throws `ArgumentException`; and `doctor` accepts no arguments, so any
extra argument throws `ArgumentException` naming it, before anything is printed. Beyond that
`doctor` is built to keep reporting. Any file-system failure in the writability probe is collapsed
into one `[FAIL]` line and the `UNHEALTHY` summary. A disk-space query that fails prints
"(could not be determined)". A device probe that throws is reported as "(could not be determined)"
for that count, and a catalog enumeration that throws is reported as "Installed: (could not be
determined)"; neither changes the summary or the exit code. The native-library probe never throws.

**Dependencies**: The library's `AudioDeviceFactory`, the probe interfaces and the `Unavailable*`
probe fallbacks, `SpeechModelStore`, `SpeechModelStoreOptions`, `SpeechModelDescriptor` and
`SpeechModelState`; `ICliModelCatalog` and `CliModelCatalogFactory` from `ModelCommandsSubsystem`,
reused rather than duplicated; `PathHelpers` for the marker-file path; and the CLI's `Context`.

**Callers**: `CommandDispatch`, through its subcommand table.
