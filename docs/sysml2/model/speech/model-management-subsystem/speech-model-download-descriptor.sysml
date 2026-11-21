package Speech_SpeechModelDownloadDescriptor {
    private import SoftwareItemMetadata::*;
    private import Speech_SpeechModelDownloadFile::*;

    part def SpeechModelDownloadDescriptor {
        doc /* Describes the full set of files required to install a model: an ordered, non-empty list of
             * SpeechModelDownloadFile entries with unique relative install paths. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelDownloadDescriptor.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelDownloadDescriptorTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-download-descriptor.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-download-descriptor.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-download-descriptor.yaml");
        }

        // Lists the individual download files needed to install a model.
        dependency SpeechModelDownloadDescriptor to SpeechModelDownloadFile;
    }
}
