package Speech {
    part def SpeechModelCatalog {
        doc /* Enumerates the compiled-in known-model registry (as of Phase 7a, this phase's
             * two real recognition models) alongside each model's current install state, and
             * orchestrates downloading a known model by id through SpeechModelDownloader,
             * layering Downloading/FailedOrCorrupt tracking scoped to one catalog instance's
             * lifetime on top of the store's bare installed/not-installed state. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelCatalog.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelCatalogTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/speech-model-catalog.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/speech-model-catalog.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/speech-model-catalog.yaml */
    }
}
