package Speech {
    part def PortAudioEnvironment {
        doc /* Shared production environment and policy helper that caches one PortAudio
             * initialization attempt and resolves the current platform's preferred host API. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/PortAudio/PortAudioEnvironment.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudio/PortAudioEnvironmentTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/port-audio/port-audio-environment.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/port-audio/port-audio-environment.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/port-audio/port-audio-environment.yaml */
    }
}
