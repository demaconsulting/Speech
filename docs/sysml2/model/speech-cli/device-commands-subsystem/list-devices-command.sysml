package SpeechCli {
    part def ListDevicesCommand {
        doc /* Implements list-devices: enumerates capture/playback probes, optionally
             * filtered by --direction, and prints an aligned table or a "no devices found"
             * message. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/DeviceCommandsSubsystem/ListDevicesCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/DeviceCommandsSubsystem/ListDevicesCommandTests.cs */
        comment designRef /* Design: docs/design/speech-cli/device-commands-subsystem.md (documented within the subsystem doc; no dedicated unit doc) */
        comment verificationRef /* Verification: docs/verification/speech-cli/device-commands-subsystem.md (verified within the subsystem doc; no dedicated unit doc) */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/device-commands-subsystem.yaml (requirements held in the subsystem file; no dedicated unit file) */
    }
}
