package SpeechCli_ModelCommandsSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_CleanCommand::*;
    private import SpeechCli_CliModelCatalogFactory::*;
    private import SpeechCli_DownloadCommand::*;
    private import SpeechCli_DownloadProgressReporter::*;
    private import SpeechCli_ICliModelCatalog::*;
    private import SpeechCli_ListModelsCommand::*;
    private import SpeechCli_ListVoicesCommand::*;
    private import SpeechCli_ModelInfoCommand::*;
    private import SpeechCli_SpeechModelCatalogAdapter::*;
    private import SpeechCli_UninstallCommand::*;
    private import SpeechCli_VoicesCommand::*;

    part def ModelCommandsSubsystem {
        doc /* The five model-management subcommands (list-models, model-info, download, uninstall, clean) and the two
             * reference-voice subcommands (list-voices, voices), plus the CLI-owned catalog seam
             * (ICliModelCatalog/SpeechModelCatalogAdapter/CliModelCatalogFactory) they all share over the library's
             * SpeechModelCatalog/SpeechModelStore. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-cli/model-commands-subsystem.md");
            verificationFiles = ("docs/verification/speech-cli/model-commands-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-cli/model-commands-subsystem.yaml");
        }

        part iCliModelCatalog : ICliModelCatalog;
        part speechModelCatalogAdapter : SpeechModelCatalogAdapter;
        part cliModelCatalogFactory : CliModelCatalogFactory;
        part listModelsCommand : ListModelsCommand;
        part modelInfoCommand : ModelInfoCommand;
        part downloadCommand : DownloadCommand;
        part downloadProgressReporter : DownloadProgressReporter;
        part uninstallCommand : UninstallCommand;
        part cleanCommand : CleanCommand;
        part listVoicesCommand : ListVoicesCommand;
        part voicesCommand : VoicesCommand;
    }
}
