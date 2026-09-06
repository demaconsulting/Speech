package Speech {
    part def UnavailableAudioPlaybackDevice {
        doc /* Honest "unavailable" IAudioPlaybackDevice fallback: reports IsAvailable as false and
             * throws AudioDeviceUnavailableException only when an operational member is invoked. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/UnavailableAudioPlaybackDevice.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/UnavailableAudioDeviceTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem.md (documented as a supporting fallback type) */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem.md (documented as a supporting fallback type) */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/unavailable-fallbacks.yaml */
    }
}
