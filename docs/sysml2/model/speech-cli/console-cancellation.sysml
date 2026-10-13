package SpeechCli {
    part def ConsoleCancellation {
        doc /* Supporting internal static helper that wires one Ctrl+C handler to a cancellation
             * token for the commands that own cancellation, and unregisters it on disposal. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Cli/ConsoleCancellation.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Cli/ConsoleCancellationTests.cs */
        comment designRef /* Design: docs/design/speech-cli.md (documented as a supporting type) */
                comment verificationRef /* Verification: docs/verification/speech-cli.md (verified within the system doc; no dedicated unit doc) */
                comment reqRef /* Requirements: docs/reqstream/speech-cli.yaml (requirements held in the system file; no dedicated unit file) */    }
}
