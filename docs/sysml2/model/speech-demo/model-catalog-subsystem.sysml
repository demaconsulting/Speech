package SpeechDemo {
    part def ModelCatalogSubsystem {
        doc /* The model catalog and download panel: a demo-owned catalog seam over the
             * library's SpeechModelCatalog, the list presentation with per-model install
             * state, download progress and honest failure reporting, and the explanatory
             * empty-catalog message shown while the library ships no downloadable models, plus
             * the reference voice pack section (install/remove, consent note, user voices
             * folder) whose changes rebuild the catalog and raise ReferenceVoicesChanged. */

        comment designRef /* Design: docs/design/speech-demo/model-catalog-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-catalog-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-catalog-subsystem.yaml */

        part iModelCatalogService : IModelCatalogService;
        part modelCatalogService : ModelCatalogService;
        part modelCatalogViewModel : ModelCatalogViewModel;
        part modelListItemViewModel : ModelListItemViewModel;
        part referenceVoicePackListItemViewModel : ReferenceVoicePackListItemViewModel;
        part modelCatalogComposition : ModelCatalogComposition;
    }
}
