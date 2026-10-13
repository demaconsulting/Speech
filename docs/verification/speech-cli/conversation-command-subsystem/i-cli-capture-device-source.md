### ICliCaptureDeviceSource Verification

#### Verification Approach

`ICliCaptureDeviceSource` has no executable behavior of its own, so the contract is verified through its
implementations. The production `AudioDeviceFactoryCaptureDeviceSource` proves the contract against the real
`AudioDeviceFactory`; its tests are listed under _AudioDeviceFactoryCaptureDeviceSource Verification_. The
in-memory `FakeCaptureDeviceSource`, which implements the same interface for `AskCommand`'s and `RecognizeCommand`'s
tests, proves that commands can run a complete turn against a substituted seam; those tests are listed under
_AskCommand Verification_.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner; no audio hardware is required.

#### Acceptance Criteria

The contract is verified when the production implementation exposes the capture probe of the factory it wraps and
creates the same device the factory creates for a null or named selection, and when `AskCommand` completes a full
turn through the test double implementing the interface.

#### Test Scenarios

The scenarios that verify this interface are:

- `AudioDeviceFactoryCaptureDeviceSource_CaptureProbe_ForwardsToFactory`: the probe the interface exposes is the
  factory's own probe.
- `AudioDeviceFactoryCaptureDeviceSource_CreateCaptureDevice_ForwardsToFactory`: device creation for the default
  and for a named selection returns what the factory returns.
- `AskCommand_Run_Success_SpeaksThenListensAndPrintsFinalResult`: a complete turn runs through the interface when
  a test double supplies the device.
