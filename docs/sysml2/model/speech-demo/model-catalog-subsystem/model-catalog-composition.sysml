package SpeechDemo {
    part def ModelCatalogComposition {
        doc /* Supporting record pairing one composed SpeechModelCatalog with the single
             * ReferenceVoiceLibrary created alongside it, so a catalog rebuild always replaces both. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelCatalogSubsystem/ModelCatalogComposition.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ModelCatalogSubsystem/ModelCatalogServiceTests.cs */
        comment designRef /* Design: docs/design/speech-demo/model-catalog-subsystem.md (documented as a supporting type) */
                comment verificationRef /* Verification: docs/verification/speech-demo/model-catalog-subsystem.md (verified within the subsystem doc; no dedicated unit doc) */
                comment reqRef /* Requirements: docs/reqstream/speech-demo/model-catalog-subsystem.yaml (requirements held in the subsystem file; no dedicated unit file) */    }
}
