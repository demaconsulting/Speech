package Speech_SpeechModelCatalog {
    private import SoftwareItemMetadata::*;
    private import Speech_IModelDownloadClient::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_SpeechModelContract::*;
    private import Speech_SpeechModelDescriptor::*;
    private import Speech_SpeechModelDescriptorEnums::*;
    private import Speech_SpeechModelDownloadProgress::*;
    private import Speech_SpeechModelDownloader::*;
    private import Speech_SpeechModelDownloaderOptions::*;
    private import Speech_SpeechModelStore::*;
    private import Speech_SpeechModelStoreOptions::*;

    part def SpeechModelCatalog {
        doc /* Enumerates the models registered with it through AddModels (the library itself ships none; sibling
             * packages such as SpeechSherpa supply them) alongside each model's current install state, and
             * orchestrates downloading a known model by id through SpeechModelDownloader, layering
             * Downloading/FailedOrCorrupt tracking scoped to one catalog instance's lifetime on top of the store's
             * bare installed/not-installed state. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelCatalog.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelCatalogTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-catalog.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-catalog.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-catalog.yaml");
        }

        // Pairs registered models with their store state and drives installs through the downloader.
        dependency SpeechModelCatalog to IModelDownloadClient;
        dependency SpeechModelCatalog to ISpeechDiagnostics;
        dependency SpeechModelCatalog to SpeechModelContract;
        dependency SpeechModelCatalog to SpeechModelDescriptor;
        dependency SpeechModelCatalog to SpeechModelDescriptorEnums;
        dependency SpeechModelCatalog to SpeechModelDownloadProgress;
        dependency SpeechModelCatalog to SpeechModelDownloader;
        dependency SpeechModelCatalog to SpeechModelDownloaderOptions;
        dependency SpeechModelCatalog to SpeechModelStore;
        dependency SpeechModelCatalog to SpeechModelStoreOptions;
    }
}
