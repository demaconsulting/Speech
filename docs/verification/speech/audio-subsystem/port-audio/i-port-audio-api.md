#### IPortAudioApi

##### Verification Approach

Verified indirectly through `PortAudioEnvironment`, `PortAudioCaptureDevice`, and
`PortAudioPlaybackDevice`, each of which consumes a fake implementation in unit tests.

##### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

##### Acceptance Criteria

The seam is considered verified when fake implementations can drive host-API resolution,
capture/playback format negotiation, and capture/playback stream behavior in deterministic unit
tests.

##### Test Scenarios

See the PortAudio-level scenarios "Environment: Platform Mapping and Host API Resolution" and
"Seam: Capture and Playback Devices Interact Through Fake Streams".
