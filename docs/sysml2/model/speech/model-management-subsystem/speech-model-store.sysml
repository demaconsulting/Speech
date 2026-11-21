package Speech_SpeechModelStore {
    private import SoftwareItemMetadata::*;
    private import Speech_SpeechModelStoreException::*;
    private import Speech_SpeechModelStoreOptions::*;

    part def SpeechModelStore {
        doc /* Per-user speech model storage under a resolved root directory, providing atomic install/repair via a
             * current/ + install-manifest.json + .tmp/{guid} swap that never touches current/ until a verified
             * replacement is fully staged, honest installed-state queries, leftover-tmp cleanup (including the empty
             * .tmp root after an install), and uninstall (with awaitable UninstallAsync/CleanUpLeftoversAsync forms). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelStore.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelStoreTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-store.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-store.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-store.yaml");
        }

        // Applies the store options for its root location and raises store exceptions on failure.
        dependency SpeechModelStore to SpeechModelStoreException;
        dependency SpeechModelStore to SpeechModelStoreOptions;
    }
}
