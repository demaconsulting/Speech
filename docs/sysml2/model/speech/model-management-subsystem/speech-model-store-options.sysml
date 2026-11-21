package Speech_SpeechModelStoreOptions {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    part def SpeechModelStoreOptions {
        doc /* Host-configurable options for SpeechModelStore, currently limited to an optional override of the
             * per-user storage root path (used in place of the default LocalApplicationData-rooted resolution). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelStoreOptions.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelStoreTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-store-options.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-store-options.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-store-options.yaml");
        }

        attribute rootPathOverride : String [0..1];
    }
}
