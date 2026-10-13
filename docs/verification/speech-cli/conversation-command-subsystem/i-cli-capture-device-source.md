### ICliCaptureDeviceSource Verification

#### Verification Approach

`ICliCaptureDeviceSource` has no executable behavior of its own, so the contract is verified through its
implementations. The production `AudioDeviceFactoryCaptureDeviceSource` proves the enumeration and the
unavailable-device parts of the contract over an injected fake probe; its tests are listed under
_AudioDeviceFactoryCaptureDeviceSource Verification_. The in-memory `FakeCaptureDeviceSource`, which implements the
same interface for `AskCommand`'s and `RecognizeCommand`'s tests, proves the known-name resolution part (which needs
real hardware through the factory) and that commands can run a complete turn against a substituted seam.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner; no audio hardware is required.

#### Acceptance Criteria

The contract is verified when the capture devices can be enumerated, a known named device resolves to a usable
device, an unknown named device and the default selection with no devices resolve to an unavailable device without
throwing, and `AskCommand` completes a full turn through the test double implementing the interface.

#### Test Scenarios

The scenarios that verify this interface are:

- `AudioDeviceFactoryCaptureDeviceSource_CaptureProbe_ExposesInjectedProbe`: the devices the interface enumerates are
  those of the injected probe.
- `ICliCaptureDeviceSource_CreateCaptureDevice_KnownAndUnknownNames_ResolveAsContractRequires`: through the in-memory
  implementation, a known name resolves to a usable device and an unknown name to an unavailable one.
- `AudioDeviceFactoryCaptureDeviceSource_CreateCaptureDevice_UnknownName_ReturnsUnavailableDevice`: the production
  implementation returns an unavailable device for an unknown name without throwing.
- `AudioDeviceFactoryCaptureDeviceSource_CreateCaptureDevice_DefaultWithNoDevices_ReturnsUnavailableDevice`: the
  production implementation returns an unavailable device for the default selection when no devices exist.
- `AskCommand_Run_Success_SpeaksThenListensAndPrintsFinalResult`: a complete turn runs through the interface when a
  test double supplies the device.
