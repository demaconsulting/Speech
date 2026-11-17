## PortAudioSharp2 Verification

This document provides the verification evidence for the PortAudioSharp2 OTS software item.
Requirements for this OTS item are defined in the PortAudioSharp2 OTS Software Requirements
document.

### Required Functionality

PortAudioSharp2 provides the managed PortAudio binding used by the internal `PortAudioApi`
adapter. The library relies on it for managed PortAudio initialization, device
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

#### PortAudioApi_CaptureCallback_NativeInput_DeliversManagedSamples

**Scenario**: A native capture callback delivers an input block through the PortAudioSharp2 binding
types.

**Expected**: The adapter converts the native block into the managed samples its handler receives,
proving the repository can consume the managed binding.

#### PortAudioApi_PlaybackCallback_ProvidedSamples_AreWrittenToNativeOutput

**Scenario**: A native playback callback requests output through the PortAudioSharp2 binding types.

**Expected**: The adapter writes the provided managed samples to the native output block.

#### AudioDeviceFactory_Constructor_PortAudioInitialized_ExposesRealProbes

**Scenario**: The factory composes against a successful PortAudio environment.

**Expected**: The default capture and playback probes are the PortAudio-backed implementations.

#### AudioDeviceFactory_CreateCaptureDevice_PortAudioInitialized_ReturnsRealDevice

**Scenario**: The factory creates a capture device against a successful PortAudio environment.

**Expected**: The returned device is the real PortAudio-backed capture-device implementation.

#### AudioDeviceFactory_CreatePlaybackDevice_PortAudioInitialized_ReturnsRealDevice

**Scenario**: The factory creates a playback device against a successful PortAudio environment.

**Expected**: The returned device is the real PortAudio-backed playback-device implementation.
