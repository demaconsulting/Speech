package SpeechCli {
    part def AudioDeviceFactoryCaptureDeviceSource {
        doc /* Production ICliCaptureDeviceSource implementation, forwarding every call
             * unchanged to a real, composed AudioDeviceFactory - including its
             * hardware-detection behavior - so AskCommand's real capture-device
             * resolution is exactly what it was before this seam was introduced. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ConversationCommandSubsystem/AudioDeviceFactoryCaptureDeviceSource.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ConversationCommandSubsystem/AudioDeviceFactoryCaptureDeviceSourceTests.cs */
    }
}
