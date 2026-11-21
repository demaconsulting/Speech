package Speech_SpeechModelStoreException {
    private import SoftwareItemMetadata::*;

    part def SpeechModelStoreException {
        doc /* Signals a SpeechModelStore operation could not complete, such as an uninstall blocked by an open file
             * handle within current/. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelStoreException.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelStoreExceptionTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-store-exception.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-store-exception.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-store-exception.yaml");
        }
    }
}
