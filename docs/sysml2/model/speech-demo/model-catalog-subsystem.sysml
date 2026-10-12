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
    }

    part def IModelCatalogService {
        doc /* Demo-owned seam for enumerating known models, downloading one by id, and applying
             * a new download mirror at runtime, listing and installing reference voice packs, and
             * announcing ReferenceVoicesChanged, so the panel can be tested against controlled
             * catalog data. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelCatalogSubsystem/IModelCatalogService.cs */
        comment designRef /* Design: docs/design/speech-demo/model-catalog-subsystem/model-catalog-service.md (documented together with its sole implementation, ModelCatalogService) */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-catalog-subsystem/model-catalog-service.md (documented together with its sole implementation, ModelCatalogService) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-catalog-subsystem/model-catalog-service.yaml */
    }

    part def ModelCatalogService {
        doc /* Real catalog seam implementation, delegating enumeration and download to the
             * library's SpeechModelCatalog. Constructed with a catalog factory, it also owns
             * rebuilding that catalog against a new mirror via ApplyMirror, and disposing
             * whichever catalog is then current. Constructed over a catalog and reference voice
             * library composition, it also installs and uninstalls voice packs, rebuilding the
             * pair and raising ReferenceVoicesChanged. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelCatalogSubsystem/ModelCatalogService.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ModelCatalogSubsystem/ModelCatalogServiceTests.cs */
        comment designRef /* Design: docs/design/speech-demo/model-catalog-subsystem/model-catalog-service.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-catalog-subsystem/model-catalog-service.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-catalog-subsystem/model-catalog-service.yaml */
    }

    part def ModelCatalogViewModel {
        doc /* Presentation state for the catalog panel: the model rows, the selected row, the
             * refresh and download commands, the mirror-settings fields and apply command, and
             * the explanatory empty-catalog message, and the reference voice pack rows with their
             * install and remove commands. */

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

    part def ReferenceVoicePackListItemViewModel {
        doc /* One reference voice pack row: the pack identity, size, license and attribution,
             * its install state, progress and failure explanation, the include-emotions option and
             * installed-size caption, plus the derived captions and enablement values the row
             * displays. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelCatalogSubsystem/ReferenceVoicePackListItemViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ModelCatalogSubsystem/ReferenceVoicePackListItemViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/model-catalog-subsystem/reference-voice-pack-list-item-view-model.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-catalog-subsystem.md (consolidated at subsystem level) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-catalog-subsystem/reference-voice-pack-list-item-view-model.yaml */
    }
}
