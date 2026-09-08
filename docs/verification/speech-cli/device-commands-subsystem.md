## SpeechCli DeviceCommandsSubsystem Verification

### Verification Approach

The DeviceCommandsSubsystem is verified through deterministic unit tests against hand-written
fakes of `IAudioCaptureDeviceProbe`/`IAudioPlaybackDeviceProbe` (`FakeAudioCaptureDeviceProbe`/
`FakeAudioPlaybackDeviceProbe`), which let every enumeration outcome - no devices, one or more
devices, a specific device name - be produced on demand with no real audio hardware. For
`DevicesTestCommand`, the "unknown device name" error path is proven against a real
`AudioDeviceFactory` composed over those same fake probes via the factory's public constructor,
since that path is fully resolved before any real device is created. `DoctorCommand` reuses
`FakeCliModelCatalog`/`FakeSpeechModel` from `ModelCommandsSubsystem`'s own tests, alongside the
same fake audio probes, and an isolated, uniquely named temporary directory as its model-store
root. Out-of-process integration tests in `IntegrationTests.cs` invoke the built tool as a child
process for the argument-parsing and clean-error paths that matter most from an operator's
perspective, plus a full `doctor` run against an isolated `--models-dir`.

Automated tests do **not** exercise `devices test`'s real device-creation and audio I/O path
(`RunPlaybackTest`/`RunCaptureTest` bodies beyond device-name validation): `AudioDeviceFactory`'s
`CreateCaptureDevice`/`CreatePlaybackDevice` methods depend on an internal `PortAudioEnvironment`
field independent of any injected probes, so that path cannot be deterministically faked, and this
command inherently requires real audio hardware to be meaningful. This is manual/local
verification only (see _Manual / Build-Time Verification_ below), following this repository's
established convention for genuinely hardware-dependent behavior (see the
`Speech-AudioSubsystem-UnavailableFallbacks` verification document for the same convention applied
within the library itself).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every unit test constructs its own fake probes/catalog; `DoctorCommandTests` uses
  a freshly created, uniquely named temporary directory as its model-store root, deleted
  afterward via `IDisposable`
- **Test doubles**: `FakeAudioCaptureDeviceProbe`/`FakeAudioPlaybackDeviceProbe` (hand-written
  fakes implementing the library's public probe interfaces, returning a fixed device list),
  reused `FakeCliModelCatalog`/`FakeSpeechModel` from
  `test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/`

### Test Scenarios

#### ListDevicesCommand

**Tests**: `ListDevicesCommand_Run_NoFilter_ListsBothDirections`,
`ListDevicesCommand_Run_DirectionInput_ListsOnlyCaptureDevices`,
`ListDevicesCommand_Run_DirectionOutput_ListsOnlyPlaybackDevices`,
`ListDevicesCommand_Run_NoDevices_PrintsNoDevicesMessage`,
`ListDevicesCommand_Run_InvalidDirectionValue_ThrowsArgumentException`,
`ListDevicesCommand_Run_DirectionFlagMissingValue_ThrowsArgumentException`,
`ListDevicesCommand_Run_UnsupportedFlag_ThrowsArgumentException`,
`SpeechCli_ListDevicesCommand_Invoked_ExitsCleanly`,
`SpeechCli_ListDevicesCommandWithInvalidDirection_Invoked_ReturnsCleanError`

**Scenario/Expected**: No filter lists both capture and playback devices as an aligned table with
Name/Direction/Channels/Sample Rate columns; `--direction input`/`output` each filter to exactly
the matching probe's devices; an empty result prints "No devices found." rather than an empty
table; an unrecognized `--direction` value, a value-less `--direction`, or an unsupported flag are
all rejected with `ArgumentException`; both the healthy and invalid-direction paths are also
proven against the built tool, which always exits cleanly regardless of whether this machine has
real audio hardware.

**Requirement coverage**: `SpeechCli-DeviceCommands-ListDevices`.

#### DevicesTestCommand

**Tests**: `DevicesTestCommand_ParseArguments_NoFlags_DefaultsToOutputDirection`,
`DevicesTestCommand_ParseArguments_DirectionInput_ParsesToCaptureDirection`,
`DevicesTestCommand_ParseArguments_DirectionOutput_ParsesToPlaybackDirection`,
`DevicesTestCommand_ParseArguments_DeviceFlag_ParsesDeviceName`,
`DevicesTestCommand_ParseArguments_MissingSubAction_ThrowsArgumentException`,
`DevicesTestCommand_ParseArguments_UnknownSubAction_ThrowsArgumentException`,
`DevicesTestCommand_ParseArguments_InvalidDirectionValue_ThrowsArgumentException`,
`DevicesTestCommand_ParseArguments_UnsupportedFlag_ThrowsArgumentException`,
`DevicesTestCommand_ParseArguments_DeviceFlagMissingValue_ThrowsArgumentException`,
`DevicesTestCommand_ResolveDeviceSelectionOrThrow_NullDeviceName_ReturnsNull`,
`DevicesTestCommand_ResolveDeviceSelectionOrThrow_KnownDeviceName_ReturnsSelection`,
`DevicesTestCommand_ResolveDeviceSelectionOrThrow_UnknownDeviceName_ThrowsArgumentException`,
`DevicesTestCommand_Run_UnknownPlaybackDevice_ThrowsArgumentException`,
`DevicesTestCommand_Run_UnknownCaptureDevice_ThrowsArgumentException`,
`SpeechCli_DevicesCommandWithoutSubAction_Invoked_ReturnsCleanError`,
`SpeechCli_DevicesTestCommandWithUnknownDevice_Invoked_ReturnsCleanError`

**Scenario/Expected**: No flags default to testing the output direction; `--direction input`/
`output` parse to the capture/playback direction respectively; `--device <name>` parses the
requested device name; a missing or unrecognized sub-action (anything other than literal `test`),
an unrecognized `--direction` value, an unsupported flag, or a value-less `--device` are all
rejected with `ArgumentException`; a device name absent from the enumerated devices for the
requested direction is rejected with `ArgumentException` naming the device and suggesting
`list-devices`, both for the playback and capture directions, and both in-process (via a real
`AudioDeviceFactory` composed over fake probes) and, cleanly and non-zero-exit, against the built
tool.

**Requirement coverage**: `SpeechCli-DeviceCommands-DevicesTest`.

#### Tone Generation

**Tests**: `DevicesTestCommand_GenerateToneSamples_ReturnsExpectedSampleCount`,
`DevicesTestCommand_GenerateToneSamples_AllSamplesWithinNormalizedRange`,
`DevicesTestCommand_GenerateToneSamples_IdenticalAcrossChannels`,
`DevicesTestCommand_GenerateToneSamples_NonPositiveSampleRate_ThrowsArgumentOutOfRangeException`

**Scenario/Expected**: The generated sample array has exactly `sampleRate * durationSeconds *
channelCount` entries; every sample falls within `[-1.0, 1.0]`; every channel within a frame
carries an identical value; a non-positive sample rate (or, by the same guard, channel count,
duration, or frequency) throws `ArgumentOutOfRangeException`. This proves the sine-wave math
underlying the output-direction test tone entirely in-process, with no audio hardware.

**Requirement coverage**: `SpeechCli-DeviceCommands-ToneGeneration`.

#### DoctorCommand

**Tests**: `DoctorCommand_Run_WritableModelStore_ReportsHealthy`,
`DoctorCommand_Run_ReportsModelStoreRootPath`,
`DoctorCommand_Run_ReportsInstalledVersusKnownModelCounts`,
`DoctorCommand_Run_ReportsAudioDeviceCounts`,
`DoctorCommand_Run_UnavailablePortAudio_ReportsInformationalNoteNotFailure`,
`DoctorCommand_Run_UnwritableModelStore_ReportsUnhealthyAndNonZeroExitCode`,
`DoctorCommand_Run_ExtraArgument_ThrowsArgumentException`,
`SpeechCli_DoctorCommand_Invoked_ReportsHealthChecklist`

**Scenario/Expected**: A writable model store root reports overall `HEALTHY` and exit code `0`;
the root path, installed-versus-known model counts (backed by a `FakeCliModelCatalog` with a mix
of downloaded/not-downloaded models), and input/output audio device counts (backed by fake
probes) are all present in the output; the library's own `UnavailableAudioCaptureDeviceProbe`/
`UnavailableAudioPlaybackDeviceProbe` fallback singletons are reported as an `[INFO]` note, not a
failure, and still leave the run `HEALTHY`; a model store root occupied by a plain file (so
`Directory.CreateDirectory` fails) is reported as the sole `[FAIL]` and produces a non-zero exit
code; an unsupported extra argument throws `ArgumentException`; a full run against a real,
isolated, writable `--models-dir` reports `HEALTHY` end to end against the built tool.

**Requirement coverage**: `SpeechCli-DeviceCommands-Doctor`.

#### Null Guards and Delegation

**Tests**: `ListDevicesCommand_Run_NullContext_ThrowsArgumentNullException`,
`ListDevicesCommand_Run_NullCaptureProbe_ThrowsArgumentNullException`,
`ListDevicesCommand_Run_NullPlaybackProbe_ThrowsArgumentNullException`,
`DevicesTestCommand_ResolveDeviceSelectionOrThrow_NullKnownDevices_ThrowsArgumentNullException`,
`DevicesTestCommand_Run_NullContext_ThrowsArgumentNullException`,
`DevicesTestCommand_Run_NullFactory_ThrowsArgumentNullException`,
`DoctorCommand_Run_NullContext_ThrowsArgumentNullException`,
`DoctorCommand_Run_NullCatalog_ThrowsArgumentNullException`,
`DoctorCommand_Run_EmptyModelStoreRootPath_ThrowsArgumentException`

**Scenario/Expected**: Every command rejects a `null` context, probe, factory, or catalog
immediately with `ArgumentNullException`, and `DoctorCommand` additionally rejects a `null` or
empty model store root path with `ArgumentException`, proving no command silently proceeds with a
missing dependency.

**Requirement coverage**: `SpeechCli-DeviceCommands-LibraryDelegation`.

### Requirements Coverage

- **`SpeechCli-DeviceCommands-ListDevices`**: see _ListDevicesCommand_ above
- **`SpeechCli-DeviceCommands-DevicesTest`**: see _DevicesTestCommand_ above
- **`SpeechCli-DeviceCommands-ToneGeneration`**: see _Tone Generation_ above
- **`SpeechCli-DeviceCommands-Doctor`**: see _DoctorCommand_ above
- **`SpeechCli-DeviceCommands-LibraryDelegation`**: see _Null Guards and Delegation_ above

### Acceptance Criteria

A DeviceCommandsSubsystem test run passes when: `list-devices` lists and filters detected devices
correctly, reporting "no devices" cleanly when the result is empty; `devices test` defaults to
the output direction, rejects an unknown device name for either direction cleanly, and its tone
generation math is provably correct; `doctor` reports every check as a clear pass/fail/
informational line, treats only model-store writability as a hard failure, and exits accordingly;
and every command rejects a missing context, probe, factory, or catalog.

## Manual / Build-Time Verification

The following are verified manually for this pass, rather than by an automated test, because they
require real audio hardware or depend on the actual host machine's environment:

- `dotnet run --project src/DemaConsulting.Speech.Cli -- list-devices` against a real development
  machine lists genuinely detected devices (or "No devices found." on a machine with none) with
  sane column output
- `dotnet run --project src/DemaConsulting.Speech.Cli -- doctor` against a real development
  machine reports a sane pass/fail/informational checklist reflecting that machine's actual
  native runtime, model store, audio device, and model install state
- `dotnet run --project src/DemaConsulting.Speech.Cli -- devices test` (and `--direction input`)
  against a machine with real audio hardware actually plays an audible tone / actually records
  and reports non-trivial capture statistics
