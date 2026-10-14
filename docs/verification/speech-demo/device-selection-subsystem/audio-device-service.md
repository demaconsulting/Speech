### AudioDeviceService Verification

#### Verification Approach

`AudioDeviceService` is verified through deterministic unit tests against an `AudioDeviceFactory`
composed with NSubstitute probes, proving it returns what the library's probes report and nothing
else. Refresh delegation is observed through a factory over a fake PortAudio environment, so the
forwarded call can be seen without a real audio backend.

Automated tests do not claim proof that real hardware is enumerated; that remains a manual
verification activity on hardware-equipped machines.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own factory and adapter
- **Test doubles**: NSubstitute fakes of `IAudioCaptureDeviceProbe` and
  `IAudioPlaybackDeviceProbe`, and a fake PortAudio API behind a `PortAudioEnvironment`

#### Acceptance Criteria

The unit passes when: it rejects a missing factory; it returns exactly what the library's probes
report, reflecting each probe's current answer and returning empty lists rather than null for no
devices; it forwards a refresh to the factory; and it creates capture and playback devices through
the factory, returning the library's unavailable device when the factory knows no device.

#### Test Scenarios

##### AudioDeviceService_Constructor_NullFactory_ThrowsArgumentNullException

**Scenario**: The adapter is constructed with no device factory.

**Expected**: `ArgumentNullException`.

##### AudioDeviceService_EnumerateCaptureDevices_ProbeReportsDevices_ReturnsProbeDevices

**Scenario**: The library's capture probe reports devices.

**Expected**: The adapter returns exactly those devices, unfiltered and unsorted.

##### AudioDeviceService_EnumeratePlaybackDevices_ProbeReportsDevices_ReturnsProbeDevices

**Scenario**: The library's playback probe reports devices.

**Expected**: The adapter returns exactly those devices.

##### AudioDeviceService_Enumerate_ProbesReportNothing_ReturnsEmptyLists

**Scenario**: Both probes report nothing.

**Expected**: Empty lists rather than null, so the panel never has to null-check the library.

##### AudioDeviceService_EnumerateCaptureDevices_ProbeResultChanges_ReturnsLatestProbeDevices

**Scenario**: The capture probe reports one device on the first call and a different device on the
second.

**Expected**: Each enumeration returns the probe's answer at that moment, proving nothing is
cached.

##### AudioDeviceService_RefreshDevices_Always_DelegatesToFactoryRefreshDevices

**Scenario**: `RefreshDevices()` is called on an adapter over a factory built on a fake PortAudio
environment.

**Expected**: The call is forwarded to `AudioDeviceFactory.RefreshDevices()` exactly once, proved by
the environment being terminated once and initialized twice.

##### AudioDeviceService_CreateCaptureDevice_ProbeKnowsNoDevices_ReturnsUnavailableDevice

**Scenario**: A capture device is requested for a name the factory's probe does not report.

**Expected**: The library's shared unavailable capture device is returned.

##### AudioDeviceService_CreatePlaybackDevice_ProbeKnowsNoDevices_ReturnsUnavailableDevice

**Scenario**: A playback device is requested with no selection while the factory's probe reports
nothing.

**Expected**: The library's shared unavailable playback device is returned.
