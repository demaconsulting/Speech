package Speech {
    part def PortAudioCaptureDevice {
        doc /* Real PortAudio-backed capture device that resolves a selected or default input
             * device, exposes honest availability, and raises captured frames through the public
             * contract. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/PortAudioCaptureDevice.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudioCaptureDeviceTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/port-audio-capture-device.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/port-audio-capture-device.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/port-audio-capture-device.yaml */
    }
}
