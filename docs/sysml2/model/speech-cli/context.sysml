package SpeechCli_Context {
    private import SoftwareItemMetadata::*;

    part def Context {
        doc /* Supporting internal class that parses the command-line arguments (global options, subcommand name and
             * subcommand arguments), resolves the download mirror, and owns the program's output writers and exit
             * code. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Cli/Context.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Cli/ContextTests.cs", "test/DemaConsulting.Speech.Cli.Tests/IntegrationTests.cs");
            designFiles = ("docs/design/speech-cli/context.md");
            verificationFiles = ("docs/verification/speech-cli/context.md");
            requirementFiles = ("docs/reqstream/speech-cli/context.yaml");
        }

        // Holds parsed arguments for the dispatch table and resolves the download mirror from the mirror
        // options.
        dependency Context to SpeechCli_CommandDispatch::CommandDispatch;
        dependency Context to Speech_DownloadMirror::DownloadMirror;
    }
}
