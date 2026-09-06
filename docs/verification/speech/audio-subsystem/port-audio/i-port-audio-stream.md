#### IPortAudioStream

##### Verification Approach

Verified indirectly through `PortAudioCaptureDevice` and `PortAudioPlaybackDevice`, each of
which consumes a fake stream implementation in unit tests.

##### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

##### Acceptance Criteria

The seam is considered verified when fake streams can simulate start, stop, dispose, capture,
and playback-callback behavior deterministically.

##### Test Scenarios

See the PortAudio-level scenario "Seam: Capture and Playback Devices Interact Through Fake
Streams".
