package Speech {
    part def UnavailableAudioPlaybackDeviceProbe {
        doc /* Honest "unavailable" IAudioPlaybackDeviceProbe fallback: always enumerates zero
             * devices rather than throwing. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/UnavailableAudioPlaybackDeviceProbe.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/UnavailableAudioDeviceTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem.md (documented as a supporting fallback type) */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem.md (documented as a supporting fallback type) */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/unavailable-fallbacks.yaml */
    }
}
