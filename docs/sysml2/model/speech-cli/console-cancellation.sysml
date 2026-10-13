package SpeechCli {
    part def ConsoleCancellation {
        doc /* Supporting internal static helper that wires one Ctrl+C handler to a cancellation
             * token for the commands that own cancellation, and unregisters it on disposal. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Cli/ConsoleCancellation.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Cli/ConsoleCancellationTests.cs */
        comment designRef /* Design: docs/design/speech-cli/console-cancellation.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/console-cancellation.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/console-cancellation.yaml */
    }
}
