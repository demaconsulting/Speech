package SpeechDemo_IModelCatalogService {
    private import SoftwareItemMetadata::*;

    part def IModelCatalogService {
        doc /* Demo-owned seam for enumerating known models, downloading one by id, and applying a new download mirror
             * at runtime, listing and installing reference voice packs, and announcing ReferenceVoicesChanged, so the
             * panel can be tested against controlled catalog data. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/ModelCatalogSubsystem/IModelCatalogService.cs");
            designFiles = ("docs/design/speech-demo/model-catalog-subsystem/model-catalog-service.md");
            verificationFiles = ("docs/verification/speech-demo/model-catalog-subsystem/model-catalog-service.md");
            requirementFiles = ("docs/reqstream/speech-demo/model-catalog-subsystem/model-catalog-service.yaml");
        }

        // Defines catalog operations in terms of library descriptors, voice packs, mirrors and download
        // progress.
        dependency IModelCatalogService to Speech_DownloadMirror::DownloadMirror;
        dependency IModelCatalogService to Speech_ReferenceVoicePack::ReferenceVoicePack;
        dependency IModelCatalogService to Speech_SpeechModelDescriptor::SpeechModelDescriptor;
        dependency IModelCatalogService to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency IModelCatalogService to Speech_SpeechModelDownloadProgress::SpeechModelDownloadProgress;
        dependency IModelCatalogService to Speech_SpeechModelDownloader::SpeechModelDownloader;
    }
}
