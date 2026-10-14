package SpeechCli {
    part def DoctorCommand {
        doc /* Implements doctor: reports native runtime resolvability, model store
             * writability/disk space, audio device counts, and installed-versus-known model
             * counts as a pass/fail checklist, treating only model store writability as a
             * hard failure. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/DeviceCommandsSubsystem/DoctorCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/DeviceCommandsSubsystem/DoctorCommandTests.cs */
        comment designRef /* Design: docs/design/speech-cli/device-commands-subsystem/doctor-command.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/device-commands-subsystem/doctor-command.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/device-commands-subsystem/doctor-command.yaml */
    }
}
