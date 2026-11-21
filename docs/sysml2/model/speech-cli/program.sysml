package SpeechCli_Program {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_CommandDispatch::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_Validation::*;

    part def Program {
        doc /* The process entry point: Main wraps RunAsync, reports ArgumentException and InvalidOperationException
             * as clean non-zero-exit errors (honoring --silent), and RunAsync checks --version, --help and --validate
             * in priority order before dispatching the resolved subcommand through the dispatch table. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Program.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/ProgramTests.cs", "test/DemaConsulting.Speech.Cli.Tests/IntegrationTests.cs");
            designFiles = ("docs/design/speech-cli/program.md");
            verificationFiles = ("docs/verification/speech-cli/program.md");
            requirementFiles = ("docs/reqstream/speech-cli/program.yaml");
        }

        // Parses Context, then routes to self-validation or dispatches the subcommand; also uses reference
        // voices.
        dependency Program to CommandDispatch;
        dependency Program to Context;
        dependency Program to Validation;
        dependency Program to Speech_ReferenceVoiceLibrary::ReferenceVoiceLibrary;
    }
}
