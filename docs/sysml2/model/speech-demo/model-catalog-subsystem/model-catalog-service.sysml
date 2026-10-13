package SpeechDemo {
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
