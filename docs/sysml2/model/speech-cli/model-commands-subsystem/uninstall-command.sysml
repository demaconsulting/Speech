package SpeechCli {
    part def UninstallCommand {
        doc /* Implements uninstall: removes a model's installed files, treating a
             * never-installed model as a safe, successful no-op. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/UninstallCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/UninstallCommandTests.cs */
        comment designRef /* Design: docs/design/speech-cli/model-commands-subsystem/uninstall-command.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/model-commands-subsystem/uninstall-command.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/model-commands-subsystem/uninstall-command.yaml */
    }
}
