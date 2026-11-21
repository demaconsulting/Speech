package SpeechCli_PathHelpers {
    private import SoftwareItemMetadata::*;

    part def PathHelpers {
        doc /* Supporting internal helper providing safe path operations for the CLI commands. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Utilities/PathHelpers.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Utilities/PathHelpersTests.cs");
            designFiles = ("docs/design/speech-cli/path-helpers.md");
            verificationFiles = ("docs/verification/speech-cli/path-helpers.md");
            requirementFiles = ("docs/reqstream/speech-cli/path-helpers.yaml");
        }
    }
}
