package SpeechDemo_ModelCatalogViewModel {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_IModelCatalogService::*;
    private import SpeechDemo_MirrorOptionsFactory::*;
    private import SpeechDemo_ModelListItemViewModel::*;
    private import SpeechDemo_ReferenceVoicePackListItemViewModel::*;

    part def ModelCatalogViewModel {
        doc /* Presentation state for the catalog panel: the model rows, the selected row, the refresh and download
             * commands, the mirror-settings fields and apply command, and the explanatory empty-catalog message, and
             * the reference voice pack rows with their install and remove commands. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/ModelCatalogSubsystem/ModelCatalogViewModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/ModelCatalogSubsystem/ModelCatalogViewModelTests.cs");
            designFiles = ("docs/design/speech-demo/model-catalog-subsystem/model-catalog-view-model.md");
            verificationFiles = ("docs/verification/speech-demo/model-catalog-subsystem/model-catalog-view-model.md");
            requirementFiles = ("docs/reqstream/speech-demo/model-catalog-subsystem/model-catalog-view-model.yaml");
        }

        // Binds catalog service data to model/voice-pack rows and checks mirror settings with
        // MirrorOptionsFactory.
        dependency ModelCatalogViewModel to IModelCatalogService;
        dependency ModelCatalogViewModel to MirrorOptionsFactory;
        dependency ModelCatalogViewModel to ModelListItemViewModel;
        dependency ModelCatalogViewModel to ReferenceVoicePackListItemViewModel;
        dependency ModelCatalogViewModel to Speech_ReferenceVoiceLibrary::ReferenceVoiceLibrary;
        dependency ModelCatalogViewModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency ModelCatalogViewModel to Speech_SpeechModelDownloadProgress::SpeechModelDownloadProgress;
        dependency ModelCatalogViewModel to Speech_SpeechModelDownloader::SpeechModelDownloader;
        dependency ModelCatalogViewModel to OtsDependencies::CommunityToolkitMvvm;
    }
}
