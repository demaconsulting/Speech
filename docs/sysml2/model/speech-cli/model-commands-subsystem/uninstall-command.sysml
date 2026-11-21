package SpeechCli_UninstallCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_CliModelCatalogFactory::*;
    private import SpeechCli_ConsoleCancellation::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_ICliModelCatalog::*;

    part def UninstallCommand {
        doc /* Implements uninstall: removes a model's installed files, treating a never-installed model as a safe,
             * successful no-op. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/UninstallCommand.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/UninstallCommandTests.cs");
            designFiles = ("docs/design/speech-cli/model-commands-subsystem/uninstall-command.md");
            verificationFiles = ("docs/verification/speech-cli/model-commands-subsystem/uninstall-command.md");
            requirementFiles = ("docs/reqstream/speech-cli/model-commands-subsystem/uninstall-command.yaml");
        }

        // Uninstalls through the catalog seam and reports store failures from SpeechModelStoreException as
        // errors.
        dependency UninstallCommand to CliModelCatalogFactory;
        dependency UninstallCommand to ConsoleCancellation;
        dependency UninstallCommand to Context;
        dependency UninstallCommand to ICliModelCatalog;
        dependency UninstallCommand to Speech_SpeechModelStoreException::SpeechModelStoreException;
    }
}
