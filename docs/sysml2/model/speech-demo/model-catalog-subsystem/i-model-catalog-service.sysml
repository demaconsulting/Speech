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
}
