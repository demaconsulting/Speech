package Speech {
    part def UnavailableAudioCaptureDeviceProbe {
        doc /* Honest "unavailable" IAudioCaptureDeviceProbe fallback: always enumerates zero
             * devices rather than throwing. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/UnavailableAudioCaptureDeviceProbe.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/UnavailableAudioDeviceTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/unavailable-audio-capture-device-probe.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/unavailable-audio-capture-device-probe.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/unavailable-audio-capture-device-probe.yaml */
    }
}
