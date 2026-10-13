package SpeechCli {
    part def Context {
        doc /* Supporting internal class that parses the command-line arguments (global options,
             * subcommand name and subcommand arguments), resolves the download mirror, and owns
             * the program's output writers and exit code. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Cli/Context.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Cli/        ContextTests.cs, test/DemaConsulting.Speech.Cli.Tests/IntegrationTests.cs */
                comment designRef /* Design: docs/design/speech-cli/context.md */
                comment verificationRef /* Verification: docs/verification/speech-cli/context.md */
                comment reqRef /* Requirements: docs/reqstream/speech-cli/context.yaml */
            }
}
