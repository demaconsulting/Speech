package SpeechDemo {
    part def ModelCatalogSubsystem {
        doc /* The model catalog and download panel: a demo-owned catalog seam over the
             * library's SpeechModelCatalog, the list presentation with per-model install
             * state, download progress and honest failure reporting, and the explanatory
             * empty-catalog message shown while the library ships no downloadable models. */

        comment designRef /* Design: docs/design/speech-demo/model-catalog-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-catalog-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-catalog-subsystem.yaml */

        part iModelCatalogService : IModelCatalogService;
        part modelCatalogService : ModelCatalogService;
        part modelCatalogViewModel : ModelCatalogViewModel;
        part modelListItemViewModel : ModelListItemViewModel;
    }

    part def IModelCatalogService {
        doc /* Demo-owned seam for enumerating known models and downloading one by id, so the
             * panel can be tested against controlled catalog data. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelCatalogSubsystem/IModelCatalogService.cs */
        comment designRef /* Design: docs/design/speech-demo/model-catalog-subsystem/model-catalog-service.md (documented together with its sole implementation, ModelCatalogService) */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-catalog-subsystem/model-catalog-service.md (documented together with its sole implementation, ModelCatalogService) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-catalog-subsystem/model-catalog-service.yaml */
    }

    part def ModelCatalogService {
        doc /* Real catalog seam implementation, delegating enumeration and download to the
             * library's SpeechModelCatalog without owning its lifetime. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelCatalogSubsystem/ModelCatalogService.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ModelCatalogSubsystem/ModelCatalogServiceTests.cs */
        comment designRef /* Design: docs/design/speech-demo/model-catalog-subsystem/model-catalog-service.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-catalog-subsystem/model-catalog-service.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-catalog-subsystem/model-catalog-service.yaml */
    }

    part def ModelCatalogViewModel {
        doc /* Presentation state for the catalog panel: the model rows, the selected row, the
             * refresh and download commands, and the explanatory empty-catalog message. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelCatalogSubsystem/ModelCatalogViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ModelCatalogSubsystem/ModelCatalogViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/model-catalog-subsystem/model-catalog-view-model.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-catalog-subsystem/model-catalog-view-model.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-catalog-subsystem/model-catalog-view-model.yaml */
    }

    part def ModelListItemViewModel {
        doc /* One catalog row: a model's identity and role, its install state, its download
             * progress, and its failure explanation, plus the derived captions and
             * enablement values the row displays. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelCatalogSubsystem/ModelListItemViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ModelCatalogSubsystem/ModelListItemViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/model-catalog-subsystem/model-list-item-view-model.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-catalog-subsystem/model-list-item-view-model.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-catalog-subsystem/model-list-item-view-model.yaml */
    }
}
