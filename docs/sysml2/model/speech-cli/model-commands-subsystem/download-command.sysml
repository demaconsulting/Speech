package SpeechCli_DownloadCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_CliModelCatalogFactory::*;
    private import SpeechCli_ConsoleCancellation::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_DownloadProgressReporter::*;
    private import SpeechCli_ICliModelCatalog::*;

    part def DownloadCommand {
        doc /* Implements download: downloads one or more models sequentially, reports live progress, isolates
             * per-model failures, honors --force, and stops the whole batch (rather than continuing) on a genuine
             * Ctrl+C cancellation. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/DownloadCommand.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/DownloadCommandTests.cs");
            designFiles = ("docs/design/speech-cli/model-commands-subsystem/download-command.md");
            verificationFiles = ("docs/verification/speech-cli/model-commands-subsystem/download-command.md");
            requirementFiles = ("docs/reqstream/speech-cli/model-commands-subsystem/download-command.yaml");
        }

        // Downloads models through the catalog seam, showing progress, with console cancellation to stop.
        dependency DownloadCommand to CliModelCatalogFactory;
        dependency DownloadCommand to ConsoleCancellation;
        dependency DownloadCommand to Context;
        dependency DownloadCommand to DownloadProgressReporter;
        dependency DownloadCommand to ICliModelCatalog;
        dependency DownloadCommand to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency DownloadCommand to Speech_SpeechModelDownloader::SpeechModelDownloader;
    }
}
