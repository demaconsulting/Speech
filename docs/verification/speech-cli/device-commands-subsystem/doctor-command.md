### DoctorCommand Verification

#### Verification Approach

`DoctorCommand` is verified through deterministic unit tests that call the injectable `Run`
overload with fake audio probes, a `FakeCliModelCatalog` (with `FakeSpeechModel` values) reused from
the ModelCommandsSubsystem tests, and an isolated, uniquely named temporary directory as the model
store root. The native-library probe is verified two ways: end to end through the real load path,
and by path resolution against a fixture directory. An out-of-process test in `IntegrationTests.cs`
runs the built tool against an isolated `--models-dir`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each test uses its own fakes and a freshly created temporary directory, deleted
  through `IDisposable`. The tests redirect the process-wide console and belong to the `Sequential`
  xUnit collection, so they never run in parallel with another test that redirects the console.
- **Test doubles**: `FakeAudioCaptureDeviceProbe`/`FakeAudioPlaybackDeviceProbe` (and throwing
  variants), `FakeCliModelCatalog`/`FakeSpeechModel`
- **Native-library coupling**: `DoctorCommand_Run_ReportsSherpaOnnxNativeLibraryResolvable`
  depends on the environment rather than on a fake: it passes because the `org.k2fsa.sherpa.onnx`
  package unconditionally depends on every platform's runtime package, so the real native asset is
  transitively copied into the test project's output on every supported runner. If the asset is
  absent on a runner, the test is skipped rather than failed.

#### Acceptance Criteria

The unit passes when a writable model store yields `HEALTHY` with exit code `0`; the store path,
disk space, model counts and device counts appear in the output; the SherpaOnnx native library is
reported resolvable through the real load path and its path resolution is correct; an unavailable
PortAudio backend is an informational note that leaves the run healthy; an unwritable store is the
sole `[FAIL]` and yields a non-zero exit code; throwing probes or catalogs degrade to
"(could not be determined)" without changing the result; and a missing context, probe, catalog or
store path, an empty store path and an extra argument are rejected.

#### Test Scenarios

##### DoctorCommand_Run_WritableModelStore_ReportsHealthy

**Scenario**: The model-store root is a writable temporary directory.

**Expected**: The output contains `Overall: HEALTHY` and the exit code is `0`.

##### DoctorCommand_Run_ReportsModelStoreRootPath

**Scenario**: A run against a known store root.

**Expected**: The root path appears in the output.

##### DoctorCommand_Run_ReportsAvailableDiskSpace

**Scenario**: A run against a real temporary directory.

**Expected**: The output contains an "Available disk space:" line.

##### DoctorCommand_Run_ReportsInstalledVersusKnownModelCounts

**Scenario**: A fake catalog with a mix of downloaded and not-downloaded models.

**Expected**: The output contains "Installed: 1 / 3 known".

##### DoctorCommand_Run_ReportsAudioDeviceCounts

**Scenario**: One fake capture device and no playback devices.

**Expected**: "Input devices detected: 1" and "Output devices detected: 0".

##### DoctorCommand_Run_ReportsSherpaOnnxNativeLibraryResolvable

**Scenario**: A run through the real `TryProbeSherpaOnnxNativeLibrary`/`CheckNativeRuntimes` path.

**Expected**: The `[ OK ]` SherpaOnnx line is present (see the native-library coupling above). The test is
skipped, not failed, on a runner where the native asset is absent.

##### DoctorCommand_Run_NullCaptureProbe_ThrowsArgumentNullException

**Scenario**: The capture probe is null.

**Expected**: `ArgumentNullException`.

##### DoctorCommand_Run_NullPlaybackProbe_ThrowsArgumentNullException

**Scenario**: The playback probe is null.

**Expected**: `ArgumentNullException`.

##### DoctorCommand_ResolveSherpaOnnxNativeLibraryPath_AssetPresent_ReturnsPath

**Scenario**: A fixture directory contains the runtime-specific `runtimes/<rid>/native/<file>`.

**Expected**: The full path of that file is returned.

##### DoctorCommand_ResolveSherpaOnnxNativeLibraryPath_AssetAbsent_ReturnsNull

**Scenario**: The fixture directory has no such file.

**Expected**: `null`.

##### DoctorCommand_Run_UnavailablePortAudio_ReportsInformationalNoteNotFailure

**Scenario**: The probes are the library's `Unavailable*` fallback singletons.

**Expected**: A `[INFO] PortAudio` line, `Overall: HEALTHY` and exit code `0`.

##### DoctorCommand_Run_UnwritableModelStore_ReportsUnhealthyAndNonZeroExitCode

**Scenario**: The model-store root path is occupied by a plain file, so the directory cannot be
created.

**Expected**: A `[FAIL]` line, an `UNHEALTHY` summary and a non-zero exit code. This is the only
failing branch of `doctor`, so this one test covers the non-zero exit path completely; the tests
that expect exit code `0` cover the other branch.

##### DoctorCommand_Run_ThrowingAudioProbes_DegradesGracefullyAndStaysHealthy

**Scenario**: Both probes throw from `Enumerate()`.

**Expected**: Both counts read "(could not be determined)", the summary is `HEALTHY` and the exit
code is `0`.

##### DoctorCommand_Run_ThrowingCatalog_DegradesGracefullyAndStaysHealthy

**Scenario**: The catalog throws from `Enumerate()`.

**Expected**: "Installed: (could not be determined)", `HEALTHY` and exit code `0`.

##### DoctorCommand_Run_ExtraArgument_ThrowsArgumentException

**Scenario**: `doctor` is run with an extra argument.

**Expected**: `ArgumentException`.

##### DoctorCommand_Run_NullContext_ThrowsArgumentNullException

**Scenario**: The context is null.

**Expected**: `ArgumentNullException`.

##### DoctorCommand_Run_NullCatalog_ThrowsArgumentNullException

**Scenario**: The catalog is null.

**Expected**: `ArgumentNullException`.

##### DoctorCommand_Run_EmptyModelStoreRootPath_ThrowsArgumentException

**Scenario**: The model-store root path is empty.

**Expected**: `ArgumentException`.

##### SpeechCli_DoctorCommand_Invoked_ReportsHealthChecklist

**Scenario**: The built tool runs `doctor` against an isolated, writable `--models-dir`.

**Expected**: The checklist is printed (including the "Audio devices:" section) and the run is
healthy end to end.

#### Manual Verification

Running `dotnet run --project src/DemaConsulting.Speech.Cli -- doctor` on a real development machine
reports a checklist that matches that machine's actual native runtimes, model store, audio devices
and installed models. This is manual because the result depends on the host environment.
