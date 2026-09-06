### IAudioCaptureDevice

#### Verification Approach

`IAudioCaptureDevice` is verified indirectly through both shipped implementations:
`UnavailableAudioCaptureDevice` proves the honest-unavailable path, and `PortAudioCaptureDevice`
proves real-device availability, reported capture format, frame delivery, and first-use fault
behavior.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The contract is considered verified when the unavailable implementation reports `false`
availability, zero channel count, and zero sample rate along with correct operational exceptions,
and the real PortAudio-backed implementation reports the resolved device's own capture format and
surfaces captured frames and native first-use failures correctly through the seam.

#### Test Scenarios

See the AudioSubsystem-level scenarios "Capture Device: Selection, Availability, and Stream
Delivery", "Capture Device: Reported Capture Format", and "Unavailable Fallbacks: Honest
Degradation".
