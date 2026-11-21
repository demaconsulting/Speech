package SpeechCli_ConsoleCancellation {
    private import SoftwareItemMetadata::*;

    part def ConsoleCancellation {
        doc /* Supporting internal static helper that wires one Ctrl+C handler to a cancellation token for the
             * commands that own cancellation, and unregisters it on disposal. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Cli/ConsoleCancellation.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Cli/ConsoleCancellationTests.cs");
            designFiles = ("docs/design/speech-cli/console-cancellation.md");
            verificationFiles = ("docs/verification/speech-cli/console-cancellation.md");
            requirementFiles = ("docs/reqstream/speech-cli/console-cancellation.yaml");
        }
    }
}
