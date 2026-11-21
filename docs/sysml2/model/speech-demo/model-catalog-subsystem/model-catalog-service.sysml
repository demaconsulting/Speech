package SpeechDemo_ModelCatalogService {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_IModelCatalogService::*;
    private import SpeechDemo_ModelCatalogComposition::*;

    part def ModelCatalogService {
        doc /* Real catalog seam implementation, delegating enumeration and download to the library's
             * SpeechModelCatalog. Constructed with a catalog factory, it also owns rebuilding that catalog against a
             * new mirror via ApplyMirror, and disposing whichever catalog is then current. Constructed over a catalog
             * and reference voice library composition, it also installs and uninstalls voice packs, rebuilding the
             * pair and raising ReferenceVoicesChanged. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/ModelCatalogSubsystem/ModelCatalogService.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/ModelCatalogSubsystem/ModelCatalogServiceTests.cs");
            designFiles = ("docs/design/speech-demo/model-catalog-subsystem/model-catalog-service.md");
            verificationFiles = ("docs/verification/speech-demo/model-catalog-subsystem/model-catalog-service.md");
            requirementFiles = ("docs/reqstream/speech-demo/model-catalog-subsystem/model-catalog-service.yaml");
        }

        // Implements the catalog seam with SpeechModelCatalog and the voice library, rebuilt when the
        // mirror changes.
        dependency ModelCatalogService to IModelCatalogService;
        dependency ModelCatalogService to ModelCatalogComposition;
        dependency ModelCatalogService to Speech_DownloadMirror::DownloadMirror;
        dependency ModelCatalogService to Speech_ReferenceVoiceLibrary::ReferenceVoiceLibrary;
        dependency ModelCatalogService to Speech_ReferenceVoicePack::ReferenceVoicePack;
        dependency ModelCatalogService to Speech_SpeechModelCatalog::SpeechModelCatalog;
        dependency ModelCatalogService to Speech_SpeechModelDescriptor::SpeechModelDescriptor;
        dependency ModelCatalogService to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency ModelCatalogService to Speech_SpeechModelDownloadProgress::SpeechModelDownloadProgress;
        dependency ModelCatalogService to Speech_SpeechModelDownloader::SpeechModelDownloader;
        dependency ModelCatalogService to Speech_SpeechModelDownloaderOptions::SpeechModelDownloaderOptions;
    }
}
