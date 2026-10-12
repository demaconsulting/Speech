package SpeechCli {
    part def CommandDispatch {
        doc /* Supporting record describing one subcommand in the CLI dispatch table: its canonical
             * kebab-case name, its one-line --help flag summary and the asynchronous handler (Func<Context, Task>) that runs it. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Cli/CommandDispatch.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/ProgramTests.cs */
        comment designRef /* Design: docs/design/speech-cli.md (documented as a supporting type) */
    }
}
