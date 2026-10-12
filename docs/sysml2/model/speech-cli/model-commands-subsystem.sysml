package SpeechCli {
    part def ModelCommandsSubsystem {
        doc /* The five model-management subcommands (list-models, model-info, download,
             * uninstall, clean) and the two reference-voice subcommands (list-voices, voices),
             * plus the CLI-owned catalog seam
             * (ICliModelCatalog/SpeechModelCatalogAdapter/CliModelCatalogFactory) they all
             * share over the library's SpeechModelCatalog/SpeechModelStore. */

        comment designRef /* Design: docs/design/speech-cli/model-commands-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/model-commands-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/model-commands-subsystem.yaml */

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
