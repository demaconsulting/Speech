package Speech {
    part def PortAudioEnvironment {
        doc /* Shared production environment and policy helper that caches one PortAudio
             * initialization attempt, resolves the current platform's preferred host API, and
             * tracks currently active capture/playback streams so an on-demand device-table
             * refresh can safely reinitialize the runtime or refuse to do so while a stream is
             * active. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/PortAudio/PortAudioEnvironment.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudio/PortAudioEnvironmentTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/port-audio/port-audio-environment.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/port-audio/port-audio-environment.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/port-audio/port-audio-environment.yaml */
    }
}
