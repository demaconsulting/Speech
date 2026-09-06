package Speech {
    part def SpeechModelStoreOptions {
        doc /* Host-configurable options for SpeechModelStore, currently limited to an optional
             * override of the per-user storage root path (used in place of the default
             * LocalApplicationData-rooted resolution). */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelStoreOptions.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelStoreTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/speech-model-store-options.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/speech-model-store-options.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/speech-model-store-options.yaml */
    }
}
