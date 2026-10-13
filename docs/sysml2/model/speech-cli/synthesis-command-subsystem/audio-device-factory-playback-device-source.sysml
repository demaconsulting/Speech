package SpeechCli {
    part def AudioDeviceFactoryPlaybackDeviceSource {
        doc /* Production ICliPlaybackDeviceSource implementation, forwarding every call
             * unchanged to a real, composed AudioDeviceFactory - including its
             * hardware-detection behavior - so SpeakCommand resolves real playback devices exactly as the library does. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/SynthesisCommandSubsystem/AudioDeviceFactoryPlaybackDeviceSource.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/AudioDeviceFactoryPlaybackDeviceSourceTests.cs */
        comment designRef /* Design: docs/design/speech-cli/synthesis-command-subsystem/audio-device-factory-playback-device-source.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/synthesis-command-subsystem/audio-device-factory-playback-device-source.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/synthesis-command-subsystem/audio-device-factory-playback-device-source.yaml */
    }
}
