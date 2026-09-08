package SpeechCli {
    part def DeviceCommandsSubsystem {
        doc /* The three device-related subcommands (list-devices, devices test, doctor),
             * depending directly on the library's public AudioDeviceFactory/
             * IAudioCaptureDeviceProbe/IAudioPlaybackDeviceProbe types with no CLI-owned seam
             * wrapper, since those interfaces are already public and directly fakeable. */

        comment designRef /* Design: docs/design/speech-cli/device-commands-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/device-commands-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/device-commands-subsystem.yaml */

        part listDevicesCommand : ListDevicesCommand;
        part devicesTestCommand : DevicesTestCommand;
        part doctorCommand : DoctorCommand;
    }

    part def ListDevicesCommand {
        doc /* Implements list-devices: enumerates capture/playback probes, optionally
             * filtered by --direction, and prints an aligned table or a "no devices found"
             * message. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/DeviceCommandsSubsystem/ListDevicesCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/DeviceCommandsSubsystem/ListDevicesCommandTests.cs */
    }

    part def DevicesTestCommand {
        doc /* Implements devices test: plays an in-CLI synthesized test tone (output,
             * the default) or records a short clip and reports basic statistics (input),
             * validating any requested --device name before device creation. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/DeviceCommandsSubsystem/DevicesTestCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/DeviceCommandsSubsystem/DevicesTestCommandTests.cs */
    }

    part def DoctorCommand {
        doc /* Implements doctor: reports native runtime resolvability, model store
             * writability/disk space, audio device counts, and installed-versus-known model
             * counts as a pass/fail checklist, treating only model store writability as a
             * hard failure. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/DeviceCommandsSubsystem/DoctorCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/DeviceCommandsSubsystem/DoctorCommandTests.cs */
    }
}
