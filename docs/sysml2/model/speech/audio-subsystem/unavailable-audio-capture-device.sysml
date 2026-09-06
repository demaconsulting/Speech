package Speech {
    part def UnavailableAudioCaptureDevice {
        doc /* Honest "unavailable" IAudioCaptureDevice fallback: reports IsAvailable as false and
             * throws AudioDeviceUnavailableException only when an operational member is invoked. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/UnavailableAudioCaptureDevice.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/UnavailableAudioDeviceTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem.md (documented as a supporting fallback type) */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem.md (documented as a supporting fallback type) */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/unavailable-fallbacks.yaml */
    }
}
