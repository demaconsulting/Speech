package Speech {
    part def PortAudioPlaybackDevice {
        doc /* Real PortAudio-backed playback device that resolves a selected or default output
             * device, exposes honest availability, and drains queued samples into a callback
             * stream with zero-fill on underrun. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/PortAudioPlaybackDevice.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudioPlaybackDeviceTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/port-audio-playback-device.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/port-audio-playback-device.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/port-audio-playback-device.yaml */
    }
}
