<!-- cspell:ignore onnxruntime -->
# SpeechCli System Design

This document provides the system-level design for the SpeechCli application.

![SpeechCli Structure](SpeechCliView.svg)

## Architecture

SpeechCli is a cross-platform .NET global tool, packaged as `DemaConsulting.Speech.Cli` and
installed under the command name `speech-cli`, that exposes the Speech library's model
management, audio device inspection, text-to-speech, and speech-to-text capabilities from the
command line. Like SpeechDemo, it is a sibling system to Speech rather than a subsystem of it: it
is separately built, separately packaged, and separately versioned, it has its own users (command-
line operators and scripts rather than an interactive desktop user), and the library must never
depend on it.

This pass delivers only the tool's scaffolding: process entry point, global-option and subcommand
dispatch, self-validation, and .NET global tool packaging. It recognizes all ten planned
subcommand names for `--help` purposes; all ten subcommands are implemented as of the
RecognitionCommandSubsystem pass described below. An eleventh subcommand, `ask`, was added after
all ten scaffolded subcommands were implemented, described in the ConversationCommandSubsystem
section below.

### Global Options and Subcommand Dispatch

Rather than a general-purpose argument-parsing library (e.g. `System.CommandLine`), SpeechCli
extends the hand-rolled, single-purpose parser pattern the reference `TemplateDotNetTool` uses for
its own flag set. `Context.Create(args)` resolves a fixed set of global options - `--models-dir`,
`--verbose`/`--diagnostics`, `--silent`, `--log <file>`, `--results <file>`, `--depth <#>`,
`-v`/`--version`, `-h`/`-?`/`--help`, `--validate` - regardless of where they appear on the
command line relative to the subcommand name, because every subcommand needs the same
cross-cutting configuration. The first token that is not a recognized global option resolves the
subcommand name (`Context.Command`) against the fixed dispatch table described below; every
following token that is not itself a recognized global option is collected, unparsed, into
`Context.CommandArgs` for that subcommand's own handler to interpret.
Supplying a token that resolves to neither a global option nor a known subcommand name throws
`ArgumentException`, which `Program.Main` reports as a clean, non-zero-exit-code error - the same
convention the template already uses for its own unsupported-argument case.

### Download Mirror Options

`Context.Create(args)` also resolves four further global options, recognized at the same
any-position precedence as every other global option: `--mirror-url <url>`, `--mirror-user
<user>`, `--mirror-password <pass>`, and `--mirror-bearer-token <t>`. These exist for an operator
whose network policy blocks a model's public download host (for example `huggingface.co`) and
needs every download redirected to an internal mirror instead, without changing (or even knowing)
any individual model's hardcoded public URI.

`Context.CreateMirror()` resolves these four raw strings into a `DownloadMirror` (or `null` when
`--mirror-url` was not supplied), which every model-management subcommand that downloads a model
passes to the library's `SpeechModelDownloaderOptions`. It performs no validation itself beyond
checking that `--mirror-url` parses as an absolute URI and that `--mirror-user`/
`--mirror-password` were supplied together (both as a fast, CLI-local check before constructing
`DownloadMirror`); every other validation rule - the mirror's scheme must be `http` or `https`
with no query string, fragment, or user-info component; `--mirror-user`/`--mirror-bearer-token`
are mutually exclusive; a supplied bearer token must not be empty or whitespace-only; and a
credentialed mirror must use `https` unless its host is a loopback address - is enforced once,
centrally, by `DownloadMirror`'s own constructor (see _Speech ModelManagementSubsystem Design_),
so the CLI never duplicates that logic. Any rejection surfaces as an `ArgumentException`, which
`Program.Main` reports as a clean, non-zero-exit-code error identically to every other
command-line configuration mistake - never a stack trace.

### ModelCommandsSubsystem

This pass adds the tool's first implemented subsystem: **ModelCommandsSubsystem**, covering the
five model-management subcommands - `list-models`, `model-info`, `download`, `uninstall`, and
`clean` - and the CLI-owned catalog seam (`ICliModelCatalog`/`SpeechModelCatalogAdapter`/
`CliModelCatalogFactory`) they all share over the library's `SpeechModelCatalog`/
`SpeechModelStore`. See _SpeechCli ModelCommandsSubsystem Design_ for its full design.

### DeviceCommandsSubsystem

This pass adds **DeviceCommandsSubsystem**, covering the three device-related subcommands -
`list-devices`, `devices test`, and `doctor` - directly over the library's already-public
`IAudioCaptureDeviceProbe`/`IAudioPlaybackDeviceProbe`/`AudioDeviceFactory`, with no CLI-owned
seam wrapper (unlike `ModelCommandsSubsystem`'s `ICliModelCatalog`), because those probe
interfaces are already public and directly fakeable. See _SpeechCli DeviceCommandsSubsystem
Design_ for its full design.

### SynthesisCommandSubsystem

This pass adds **SynthesisCommandSubsystem**, covering the one text-to-speech subcommand -
`speak` - and extending `ModelCommandsSubsystem`'s `ICliModelCatalog` seam with two further
members (`GetPreferredAudioFormat`/`CreateSynthesizer`) rather than introducing a second,
competing seam. See _SpeechCli SynthesisCommandSubsystem Design_ for its full design.

### RecognitionCommandSubsystem

This pass adds **RecognitionCommandSubsystem**, covering the one speech-to-text subcommand -
`recognize` - the last of all 10 subcommands to be implemented, plus the
`SilenceTimeoutRecognizerSession` idle-timeout utility, extending `ModelCommandsSubsystem`'s
`ICliModelCatalog` seam with two further members (`GetAudioFormat`/`CreateRecognizer`) symmetric
to the synthesis pair, rather than introducing a second, competing seam. See _SpeechCli
RecognitionCommandSubsystem Design_ for its full design.

### ConversationCommandSubsystem

This pass adds **ConversationCommandSubsystem**, covering the one voice-conversation subcommand -
`ask` - which speaks a prompt and then listens for the reply, combining `speak`'s synthesis-then-
play flow and `recognize --mic`'s mic-listen flow into a single invocation. It introduces no new
`ICliModelCatalog` seam member: it reuses `SynthesisCommandSubsystem`'s `CreateSynthesizer` and
`RecognitionCommandSubsystem`'s `CreateRecognizer` members exactly as `speak` and `recognize`
already do individually. See _SpeechCli ConversationCommandSubsystem Design_ for its full design.

### Subcommand Dispatch Table

`CommandDispatch` defines the fixed, ordered set of subcommands SpeechCli recognizes:
`list-models`, `model-info`, `download`, `uninstall`, `clean`, `list-devices`, `devices`,
`doctor`, `speak`, `recognize`, and `ask`. Each entry pairs the subcommand's canonical name with a
one-line usage summary (shown by `--help`) and a handler delegate. As of this pass, all eleven
subcommands' handlers are the real `ModelCommandsSubsystem`/`DeviceCommandsSubsystem`/
`SynthesisCommandSubsystem`/`RecognitionCommandSubsystem`/`ConversationCommandSubsystem`
implementations described above - no subcommand handler remains a `NotImplementedException` stub.

## External Interfaces

SpeechCli is an application, not a library: it exposes no public API to other software. Its
external interfaces are its command-line surface and the library interfaces each subcommand
consumes.

| Interface | Direction | Format | Constraints |
| --- | --- | --- | --- |
| Command-line arguments | Inbound | Process arguments | Global options recognized anywhere |
| Console output / exit code | Outbound | stdout/stderr, exit code | `0` success, `1` reported error |
| `--validate` report | Outbound | Console text, optional `.trx`/`.xml` | CI-safe; no download/hardware required |
| `AudioDeviceFactory` (self-test) | Outbound | Constructor/method call | Consumed; never throws |
| `SpeechModelStore`/`Options` (self-test) | Outbound | Constructor/property | Resolves the model-store root |
| `SpeechModelCatalog` (model commands) | Outbound | Constructor/method call | Consumed via seam; no native runtime |
| `IAudioCaptureDeviceProbe`/`IAudioPlaybackDeviceProbe` (device commands) | Outbound | Method call | Never throws |
| Audio device hardware (`devices test`) | Bidirectional | PCM samples | Real I/O; fails cleanly if unavailable |
| `ISpeechSynthesizerEngine`/`ISynthesisSession` (`speak`) | Outbound | Method call | Real I/O; graceful fallback |
| `ISpeechRecognizerEngine`/`IRecognitionSession` (`recognize`) | Outbound | Method call | Real I/O; graceful fallback |

## Dependencies

SpeechCli has two project dependencies - the Speech library, and the SpeechSherpa library whose
`AddSherpaModels()` extension method registers the shipped sherpa-onnx models with the catalog
the CLI composes (see _SpeechSherpa Design_) - and the following NuGet dependencies:

- **`DemaConsulting.TestResults`** supplies the `.trx`/JUnit `.xml` self-validation result
  serialization used by `--validate --results <file>`, mirroring the reference
  `TemplateDotNetTool`'s own self-test reporting
- No native speech-inference runtime package is declared directly by this project, but the
  `DemaConsulting.Speech.Sherpa` project reference brings one in transitively: `Sherpa`'s own
  `org.k2fsa.sherpa.onnx` package nuspec unconditionally depends on every RID's
  `org.k2fsa.sherpa.onnx.runtime.*` package regardless of the consuming project's target RID, so
  those native assets flow into this tool's build output without an explicit package reference
  here. `DoctorCommand` probes the resulting `runtimes/<rid>/native/sherpa-onnx-c-api` asset
  directly to report whether the bundled native inference library is resolvable. The Speech
  library project reference separately carries `PortAudioSharp2`'s native runtime packages
  transitively, which the self-test's `AudioDeviceFactory` probe (and `speak`'s real playback
  path) needs

See _OTS Integration Design_ for details of any OTS items shared with other systems in this
repository.

## Risk Control Measures

N/A - SpeechCli provides no safety-critical functionality requiring risk control measures
(IEC 62304 §5.3.3). It is a command-line utility with no clinical or safety role.

## Data Flow

**Process entry and dispatch path (this pass):**

1. **Input**: The process is started with zero or more command-line arguments
2. **Parsing**: `Context.Create(args)` resolves every global option and, if present, the
   subcommand name and its raw trailing arguments
3. **Priority dispatch**: `Program.Run` checks, in order, `--version`, then prints the banner,
   then checks `--help`, then `--validate`, then falls through to subcommand dispatch; only the
   highest-priority matching action executes
4. **Output**: Version text, usage text, a self-validation report, or a subcommand's own output is
   written to stdout/stderr, and the process exits with the resulting exit code

**Self-validation path:**

1. **Input**: `--validate` is given, optionally with `--results <file>` and `--depth <#>`
2. **Checks**: version display, help display, and whether the dispatch table is well formed are
   checked in-process; an `AudioDeviceFactory` is composed and both device probes are enumerated
   to prove the audio backend composition path never throws; a `SpeechModelStore` is resolved
   (honoring `--models-dir` when given) and a small marker file is written to and deleted from its
   root to prove the model-store location is writable
3. **Output**: A markdown-formatted report of each check's pass/fail outcome is written to
   stdout, and, if `--results <file>` was given, a `.trx` or `.xml` file is written with the same
   results

## Design Constraints

- **No new library API**: This pass adds no public API to the Speech library and uses no
  internal access to it; the self-test's `AudioDeviceFactory` and `SpeechModelStore` usages are
  both already-public library entry points
- **No general-purpose argument-parsing library**: The hand-rolled `Context`/`ArgumentParser`
  pattern is a deliberate choice over `System.CommandLine` for this tool, extended (rather than
  replaced) to add subcommand dispatch on top of the template's existing global-option handling
- **Stubs are honest, not silent**: Every recognized-but-unimplemented subcommand throws
  `NotImplementedException` naming itself when dispatched, rather than silently doing nothing or
  reporting success, so a caller of this pass's build can never mistake a stub for a working
  command
- **CI-safe self-validation**: No self-test check may depend on a real, multi-hundred-megabyte
  model download or physical audio hardware being present, mirroring the same constraint the
  Speech library's own `SpeechModelCatalog`/`AudioDeviceFactory` composition guarantees satisfy

### Platform Support

| Target Framework | Runtime / Environment |
| --- | --- |
| `net10.0` | .NET 10 |

SpeechCli targets only `net10.0`, unlike the library (`net8.0`/`net9.0`/`net10.0`), because as a
`PackAsTool` package it bundles a native inference runtime (`onnxruntime`/`sherpa-onnx-c-api`) per
target framework; packing all three of the library's frameworks multiplied the bundled native
payload three-fold for no benefit, since a globally-installed tool only ever runs on one .NET
version at a time. The packed native runtime assets are further pruned to `win-x64`, `linux-x64`,
and `osx-arm64` only (see the Dependencies section above), rather than every RID the transitive
`org.k2fsa.sherpa.onnx.runtime.*` packages support (including irrelevant ones like Android),
keeping the packed tool a manageable size instead of bundling every platform's native binaries.

### Integration Patterns

- **Context object**: A single `Context` instance carries every parsed command-line option and
  the resolved subcommand name/arguments through to `Program.Run` and (in later passes) to each
  subcommand handler, mirroring the reference template's own `Context` pattern
- **Fixed dispatch table**: `CommandDispatch.Commands` is the single source of truth for which
  subcommand names exist, their `--help` usage summaries, and their handlers, so `--help` output
  and dispatch can never disagree about which subcommands are recognized
