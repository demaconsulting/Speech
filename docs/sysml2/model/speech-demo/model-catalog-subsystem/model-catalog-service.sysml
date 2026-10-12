package SpeechDemo {
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
}
