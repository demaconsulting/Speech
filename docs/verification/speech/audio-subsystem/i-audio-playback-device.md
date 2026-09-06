### IAudioPlaybackDevice

#### Verification Approach

`IAudioPlaybackDevice` is verified indirectly through both shipped implementations:
`UnavailableAudioPlaybackDevice` proves the honest-unavailable path, and `PortAudioPlaybackDevice`
proves queue-based playback behavior, format disclosure, and first-use fault behavior.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The contract is considered verified when the unavailable implementation reports `false`
availability, `0` channel count/sample rate, `0` pending sample count, and operational
exceptions correctly, and the real PortAudio-backed implementation queues samples, zero-fills
underruns, reports the resolved channel count/sample rate (or `0` when unresolved), reports
pending sample count honestly as it drains, and surfaces native first-use failures correctly.

#### Test Scenarios

See the AudioSubsystem-level scenarios "Playback Device: Selection, Availability, and Queued
Playback", "Playback Device: Reported Playback Format", "Playback Device: Pending Sample Drain
Tracking", and "Unavailable Fallbacks: Honest Degradation".
