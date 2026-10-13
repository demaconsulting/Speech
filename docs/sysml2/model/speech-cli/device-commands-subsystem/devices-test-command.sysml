package SpeechCli {
    part def DevicesTestCommand {
        doc /* Implements devices test: plays an in-CLI synthesized test tone (output,
             * the default) or records a short clip and reports basic statistics (input),
             * validating any requested --device name before device creation. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/DeviceCommandsSubsystem/DevicesTestCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/DeviceCommandsSubsystem/DevicesTestCommandTests.cs */
        comment designRef /* Design: docs/design/speech-cli/device-commands-subsystem.md (documented within the subsystem doc; no dedicated unit doc) */
        comment verificationRef /* Verification: docs/verification/speech-cli/device-commands-subsystem.md (verified within the subsystem doc; no dedicated unit doc) */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/device-commands-subsystem.yaml (requirements held in the subsystem file; no dedicated unit file) */
    }
}
