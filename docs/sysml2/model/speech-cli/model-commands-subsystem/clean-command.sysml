package SpeechCli_CleanCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_CliModelCatalogFactory::*;
    private import SpeechCli_ConsoleCancellation::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_ICliModelCatalog::*;

    part def CleanCommand {
        doc /* Implements clean: removes any partial/stale download artifacts for a model, never failing for a model
             * with nothing to clean up. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/CleanCommand.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/CleanCommandTests.cs");
            designFiles = ("docs/design/speech-cli/model-commands-subsystem/clean-command.md");
            verificationFiles = ("docs/verification/speech-cli/model-commands-subsystem/clean-command.md");
            requirementFiles = ("docs/reqstream/speech-cli/model-commands-subsystem/clean-command.yaml");
        }

        // Uses the catalog from the factory to clean leftover download files; console cancellation can
        // stop it.
        dependency CleanCommand to CliModelCatalogFactory;
        dependency CleanCommand to ConsoleCancellation;
        dependency CleanCommand to Context;
        dependency CleanCommand to ICliModelCatalog;
    }
}
