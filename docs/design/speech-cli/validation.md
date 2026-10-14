## Validation

**Purpose**: Run the CLI's built-in, CI-safe self-validation (`speech-cli --validate`), print a
markdown report, and optionally write the results as a TRX or JUnit file. No check needs a
downloaded model or real audio hardware, so it can run on any machine to confirm the installed tool
works.

**Data Model**: Stateless static class with one entry point, `RunAsync(context)`. Each check produces
one `DemaConsulting.TestResults.TestResult` (class name `Validation`, code base `SpeechCli`) holding
the check's name, outcome, duration and error message; the results are collected in one
`TestResults` named `Speech CLI Self-Validation`. A private nested `TemporaryDirectory` creates a
uniquely named directory under the system temporary folder (through `PathHelpers.SafePathCombine`)
and deletes it on disposal, ignoring cleanup errors.

**Key Methods**: `RunAsync` rejects a null context with `ArgumentNullException`, then prints the
header and runs the checks in this order:

- **Version** (`SpeechCli_VersionDisplay`): runs `Program.RunAsync` over an inner context built from
  `--silent --log <temp file> --version` with an empty environment reader; passes when the exit code
  is 0 and the log contains a `n.n.n` version pattern.
- **Help** (`SpeechCli_HelpDisplay`): the same with `--help`; passes when the exit code is 0 and the
  log contains `Usage:`, `Commands:` and the name of every `CommandDispatch` entry.
- **Dispatch table** (`SpeechCli_DispatchTableWellFormed`): the table is non-empty and every entry
  has a unique non-empty name, a non-empty usage summary and a non-null handler.
- **Audio backend** (`SpeechCli_AudioBackendProbe`): constructs the library's `AudioDeviceFactory`
  and enumerates both the capture and playback probes without throwing; it does not require any
  device to be found.
- **Model store** (`SpeechCli_ModelStoreRootWritable`): resolves the model-store root (`--models-dir`
  when given, otherwise the library default), creates it if needed, writes an empty marker file
  named `.speech-cli-validate-{guid}.tmp` into it through `PathHelpers.SafePathCombine`, and deletes
  it.

The empty environment reader keeps ambient `DEMASPEECH_MIRROR_*` variables from making the version and
help checks fail. After the checks it prints `Total Tests`, `Passed` and `Failed` lines. The report
header uses a markdown heading of `Context.HeadingDepth` levels followed by a table with the tool
version, machine name, operating system, .NET runtime and a UTC time stamp.

Results file: when `Context.ResultsFile` is set, the results are serialized after the summary. A
`.trx` extension selects the TRX format and a `.xml` extension selects JUnit XML; the extension
comparison ignores case. The write is attempted even when checks failed, so a failing run is
recorded.

**Error Handling**: Every check catches any exception and records it as a failed result (`✗ {name} -
FAILED: {message}`) so one failing check never aborts the run. Failures are reported through
`Context.WriteError`, so `Context.ExitCode` becomes `1`: each failed check, the `Failed: n` line when
`n` is above zero, an unsupported results file extension (`Error: Unsupported results file format
'{extension}'. Use .trx or .xml extension.`, with no file written), and any failure to write the
results file (`Error: Failed to write results file: {message}`). An unsupported extension is
therefore detected only after the checks have run. A passing check prints `✓ {name} - Passed`.

**Dependencies**: `Context` (output, options), `Program` (version string and the inner action runs),
`CommandDispatch` (table and help names), `PathHelpers` (contained temporary paths), the library's
`AudioDeviceFactory` and `SpeechModelStore`/`SpeechModelStoreOptions`, and the
`DemaConsulting.TestResults` package for result objects and TRX/JUnit serialization.

**Callers**: `Program.RunAsync`, when `--validate` is the highest-priority action.
