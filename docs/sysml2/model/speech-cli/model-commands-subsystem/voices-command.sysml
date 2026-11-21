package SpeechCli_VoicesCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_CliModelCatalogFactory::*;
    private import SpeechCli_ConsoleCancellation::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_DownloadProgressReporter::*;
    private import SpeechCli_ICliModelCatalog::*;

    part def VoicesCommand {
        doc /* Implements voices packs|install|uninstall: lists reference-voice packs, installs one with mirror-aware
             * progress and cancellation (emotion variants by default, base voices only with --no-emotions), shows
             * download and installed sizes, or removes it, printing the consent note. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/VoicesCommand.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/VoicesCommandTests.cs");
            designFiles = ("docs/design/speech-cli/model-commands-subsystem/voices-command.md");
            verificationFiles = ("docs/verification/speech-cli/model-commands-subsystem/voices-command.md");
            requirementFiles = ("docs/reqstream/speech-cli/model-commands-subsystem/voices-command.yaml");
        }

        // Manages voice packs via the catalog and voice library, showing progress and supporting
        // cancellation.
        dependency VoicesCommand to CliModelCatalogFactory;
        dependency VoicesCommand to ConsoleCancellation;
        dependency VoicesCommand to Context;
        dependency VoicesCommand to DownloadProgressReporter;
        dependency VoicesCommand to ICliModelCatalog;
        dependency VoicesCommand to Speech_ReferenceVoiceLibrary::ReferenceVoiceLibrary;
        dependency VoicesCommand to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency VoicesCommand to Speech_SpeechModelDownloader::SpeechModelDownloader;
        dependency VoicesCommand to Speech_SpeechModelStoreException::SpeechModelStoreException;
    }
}
