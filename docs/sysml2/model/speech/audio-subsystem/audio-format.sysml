package Speech {
    part def AudioFormat {
        doc /* Immutable validating value that carries sample rate and channel count together, so
             * model declarations and audio-device composition can exchange audio-format intent
             * without leaking any backend-native type into the public API. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/AudioFormat.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/AudioFormatTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/audio-format.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/audio-format.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/audio-format.yaml */
    }
}
