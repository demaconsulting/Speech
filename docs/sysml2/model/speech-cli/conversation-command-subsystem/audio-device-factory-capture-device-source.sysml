package SpeechCli {
    part def AudioDeviceFactoryCaptureDeviceSource {
        doc /* Production ICliCaptureDeviceSource implementation, forwarding every call
             * unchanged to a real, composed AudioDeviceFactory - including its
             * hardware-detection behavior - so AskCommand's real capture-device
             * resolution is exactly what it was before this seam was introduced. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ConversationCommandSubsystem/AudioDeviceFactoryCaptureDeviceSource.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ConversationCommandSubsystem/AudioDeviceFactoryCaptureDeviceSourceTests.cs */
        comment designRef /* Design: docs/design/speech-cli/conversation-command-subsystem.md (documented within the subsystem doc; no dedicated unit doc) */
        comment verificationRef /* Verification: docs/verification/speech-cli/conversation-command-subsystem.md (verified within the subsystem doc; no dedicated unit doc) */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/conversation-command-subsystem.yaml (requirements held in the subsystem file; no dedicated unit file) */
    }
}
