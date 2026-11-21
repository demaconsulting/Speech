package SpeechCli_Validation {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_CommandDispatch::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_PathHelpers::*;

    part def Validation {
        doc /* Supporting internal static class that runs the CLI's built-in self-validation checks and writes the
             * results as TRX or JUnit XML. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/SelfTest/Validation.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/ProgramTests.cs", "test/DemaConsulting.Speech.Cli.Tests/IntegrationTests.cs");
            designFiles = ("docs/design/speech-cli/validation.md");
            verificationFiles = ("docs/verification/speech-cli/validation.md");
            requirementFiles = ("docs/reqstream/speech-cli/validation.yaml");
        }

        // Runs self-checks on the dispatch, model store and audio factory, and writes results via
        // TestResults.
        dependency Validation to CommandDispatch;
        dependency Validation to Context;
        dependency Validation to PathHelpers;
        dependency Validation to SpeechCli_Program::Program;
        dependency Validation to Speech_AudioDeviceFactory::AudioDeviceFactory;
        dependency Validation to Speech_SpeechModelStore::SpeechModelStore;
        dependency Validation to Speech_SpeechModelStoreOptions::SpeechModelStoreOptions;
        dependency Validation to OtsDependencies::TestResults;
    }
}
