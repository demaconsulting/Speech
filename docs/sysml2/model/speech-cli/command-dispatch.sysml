package SpeechCli {
    part def CommandDispatch {
        doc /* Supporting record describing one subcommand in the CLI dispatch table: its canonical
             * kebab-case name, its one-line --help flag summary and the asynchronous handler (Func<Context, Task>) that runs it. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Cli/CommandDispatch.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Cli/CommandDispatchTests.cs */
        comment designRef /* Design: docs/design/speech-cli.md (documented as a supporting type) */
                comment verificationRef /* Verification: docs/verification/speech-cli.md (verified within the system doc; no dedicated unit doc) */
                comment reqRef /* Requirements: docs/reqstream/speech-cli.yaml (requirements held in the system file; no dedicated unit file) */    }
}
