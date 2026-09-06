package Speech {
    part def SpeechModelStore {
        doc /* Per-user speech model storage under a resolved root directory, providing atomic
             * install/repair via a current/ + install-manifest.json + .tmp/{guid} swap that
             * never touches current/ until a verified replacement is fully staged, honest
             * installed-state queries, leftover-tmp cleanup, and uninstall. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelStore.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelStoreTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/speech-model-store.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/speech-model-store.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/speech-model-store.yaml */
    }
}
