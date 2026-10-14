package SpeechCli {
    part def ListDevicesCommand {
        doc /* Implements list-devices: enumerates capture/playback probes, optionally
             * filtered by --direction, and prints an aligned table or a "no devices found"
             * message. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/DeviceCommandsSubsystem/ListDevicesCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/DeviceCommandsSubsystem/ListDevicesCommandTests.cs */
        comment designRef /* Design: docs/design/speech-cli/device-commands-subsystem/list-devices-command.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/device-commands-subsystem/list-devices-command.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/device-commands-subsystem/list-devices-command.yaml */
    }
}
