package Speech {
    part def AudioDeviceFactory {
        doc /* Composition entry point for obtaining audio capture/playback devices and probes;
             * exposes real PortAudio-backed defaults when the runtime initializes and otherwise
             * degrades safely to the Unavailable* fallbacks without throwing at composition. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/AudioDeviceFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/AudioDeviceFactoryTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/audio-device-factory.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/audio-device-factory.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/audio-device-factory.yaml */
    }
}
