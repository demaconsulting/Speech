package SpeechCli {
    part def ListModelsCommand {
        doc /* Implements list-models: filters the catalog by --role/--state and prints an
             * aligned table or, with --format json, a JSON array. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/ListModelsCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/ListModelsCommandTests.cs */
        comment designRef /* Design: docs/design/speech-cli/model-commands-subsystem/list-models-command.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/model-commands-subsystem/list-models-command.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/model-commands-subsystem/list-models-command.yaml */
    }
}
