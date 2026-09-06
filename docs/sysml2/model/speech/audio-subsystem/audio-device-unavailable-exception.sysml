package Speech {
    part def AudioDeviceUnavailableException {
        doc /* Exception thrown when an operational member of an unavailable audio device or probe
             * is invoked despite IsAvailable reporting false. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/AudioDeviceUnavailableException.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/UnavailableAudioDeviceTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem.md (documented as a supporting fallback type) */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem.md (documented as a supporting fallback type) */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/unavailable-fallbacks.yaml */
    }
}
