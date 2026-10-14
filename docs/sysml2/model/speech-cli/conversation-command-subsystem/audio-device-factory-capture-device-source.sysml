package SpeechCli {
    part def AudioDeviceFactoryCaptureDeviceSource {
        doc /* Production ICliCaptureDeviceSource implementation, forwarding every call
             * unchanged to a real, composed AudioDeviceFactory - including its
             * hardware-detection behavior - so real capture-device resolution is
             * exactly the factory's own. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ConversationCommandSubsystem/AudioDeviceFactoryCaptureDeviceSource.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ConversationCommandSubsystem/AudioDeviceFactoryCaptureDeviceSourceTests.cs */
        comment designRef /* Design: docs/design/speech-cli/conversation-command-subsystem/audio-device-factory-capture-device-source.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/conversation-command-subsystem/audio-device-factory-capture-device-source.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/conversation-command-subsystem/audio-device-factory-capture-device-source.yaml */
    }
}
