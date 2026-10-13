package SpeechCli {
    part def Program {
        doc /* The process entry point: Main wraps RunAsync, reports ArgumentException and
             * InvalidOperationException as clean non-zero-exit errors (honoring --silent), and
             * RunAsync checks --version, --help and --validate in priority order before
             * dispatching the resolved subcommand through the dispatch table. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Program.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/ProgramTests.cs */
        comment designRef /* Design: docs/design/speech-cli.md (documented within the system doc; no dedicated unit doc) */
        comment verificationRef /* Verification: docs/verification/speech-cli.md (verified within the system doc; no dedicated unit doc) */
        comment reqRef /* Requirements: docs/reqstream/speech-cli.yaml (requirements held in the system file; no dedicated unit file) */
    }
}
