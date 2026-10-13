package SpeechCli {
    part def ListVoicesCommand {
        doc /* Implements list-voices: prints every available reference voice as a table or, with
             * --format json, a JSON array (with a Moods column / moods array of emotion variants), plus
             * how to choose a voice and the consent note. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/ListVoicesCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/ListVoicesCommandTests.cs */
        comment designRef /* Design: docs/design/speech-cli/model-commands-subsystem/list-voices-command.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/model-commands-subsystem/list-voices-command.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/model-commands-subsystem/list-voices-command.yaml */
    }
}
