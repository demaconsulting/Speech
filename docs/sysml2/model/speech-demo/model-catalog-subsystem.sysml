package SpeechDemo_ModelCatalogSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_IModelCatalogService::*;
    private import SpeechDemo_ModelCatalogComposition::*;
    private import SpeechDemo_ModelCatalogService::*;
    private import SpeechDemo_ModelCatalogViewModel::*;
    private import SpeechDemo_ModelListItemViewModel::*;
    private import SpeechDemo_ReferenceVoicePackListItemViewModel::*;

    part def ModelCatalogSubsystem {
        doc /* The model catalog and download panel: a demo-owned catalog seam over the library's SpeechModelCatalog,
             * the list presentation with per-model install state, download progress and honest failure reporting, and
             * the explanatory empty-catalog message shown while the library ships no downloadable models, plus the
             * reference voice pack section (install/remove, consent note, user voices folder) whose changes rebuild
             * the catalog and raise ReferenceVoicesChanged. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-demo/model-catalog-subsystem.md");
            verificationFiles = ("docs/verification/speech-demo/model-catalog-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-demo/model-catalog-subsystem.yaml");
        }

        part iModelCatalogService : IModelCatalogService;
        part modelCatalogService : ModelCatalogService;
        part modelCatalogViewModel : ModelCatalogViewModel;
        part modelListItemViewModel : ModelListItemViewModel;
        part referenceVoicePackListItemViewModel : ReferenceVoicePackListItemViewModel;
        part modelCatalogComposition : ModelCatalogComposition;
    }
}
