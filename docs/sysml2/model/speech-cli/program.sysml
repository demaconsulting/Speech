package SpeechCli {
    part def Program {
        doc /* The process entry point: Main wraps RunAsync, reports ArgumentException and
             * InvalidOperationException as clean non-zero-exit errors (honoring --silent), and
             * RunAsync checks --version, --help and --validate in priority order before
             * dispatching the resolved subcommand through the dispatch table. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Program.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/ProgramTests.cs, test/DemaConsulting.Speech.Cli.Tests/IntegrationTests.cs */
        comment designRef /* Design: docs/design/speech-cli/program.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/program.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/program.yaml */
    }
}
