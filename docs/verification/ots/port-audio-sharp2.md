## PortAudioSharp2 Verification

This document provides the verification evidence for the PortAudioSharp2 OTS software item.
Requirements for this OTS item are defined in the PortAudioSharp2 OTS Software Requirements
document.

### Required Functionality

PortAudioSharp2 provides the managed PortAudio binding used by the internal `PortAudioApi`
adapter. In this phase, the library relies on it for managed PortAudio initialization, device
metadata access, and callback-stream construction.

### Verification Approach

Automated verification for this OTS item is intentionally limited to deterministic integration
behaviors that do not require real audio hardware:

- The default shared `PortAudioApi` adapter and `PortAudioEnvironment` singletons can be loaded
  and reused
- `AudioDeviceFactory` can expose real PortAudio-backed probes and devices when the PortAudio
  environment reports successful initialization

Automated tests do **not** claim proof of true end-to-end microphone or speaker hardware I/O.
Opening a real device and moving audio through it requires manual/local verification on a machine
with supported audio hardware.

### Test Scenarios

#### PortAudioApi_Instance_ReadTwice_ReturnsSameInstance

**Scenario**: The managed PortAudio adapter singleton is read twice.

**Expected**: Both reads return the same object, proving the repository can load and reuse the
managed adapter instance.

**Requirement coverage**: `Speech-OTS-PortAudioSharp2-ManagedBinding`.

#### PortAudioEnvironment_Shared_ReadTwice_ReturnsSameInstance

**Scenario**: The shared production PortAudio environment is read twice.

**Expected**: Both reads return the same object, proving the repository can load and reuse the
managed PortAudio environment.

**Requirement coverage**: `Speech-OTS-PortAudioSharp2-ManagedBinding`.

#### AudioDeviceFactory_Constructor_PortAudioInitialized_ExposesRealProbes

**Scenario**: The factory composes against a successful PortAudio environment.

**Expected**: The default capture and playback probes are the PortAudio-backed implementations.

**Requirement coverage**: `Speech-OTS-PortAudioSharp2-HostApiSelection`.

#### AudioDeviceFactory_CreateCaptureDevice_PortAudioInitialized_ReturnsRealDevice

**Scenario**: The factory creates a capture device against a successful PortAudio environment.

**Expected**: The returned device is the real PortAudio-backed capture-device implementation.

**Requirement coverage**: `Speech-OTS-PortAudioSharp2-StreamInterop`.

#### AudioDeviceFactory_CreatePlaybackDevice_PortAudioInitialized_ReturnsRealDevice

**Scenario**: The factory creates a playback device against a successful PortAudio environment.

**Expected**: The returned device is the real PortAudio-backed playback-device implementation.

**Requirement coverage**: `Speech-OTS-PortAudioSharp2-StreamInterop`.

### Requirements Coverage

- **`Speech-OTS-PortAudioSharp2-ManagedBinding`**:
  `PortAudioApi_Instance_ReadTwice_ReturnsSameInstance`,
  `PortAudioEnvironment_Shared_ReadTwice_ReturnsSameInstance`
- **`Speech-OTS-PortAudioSharp2-HostApiSelection`**:
  `AudioDeviceFactory_Constructor_PortAudioInitialized_ExposesRealProbes`
- **`Speech-OTS-PortAudioSharp2-StreamInterop`**:
  `AudioDeviceFactory_CreateCaptureDevice_PortAudioInitialized_ReturnsRealDevice`,
  `AudioDeviceFactory_CreatePlaybackDevice_PortAudioInitialized_ReturnsRealDevice`
