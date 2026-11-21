package Speech_SpeechModelDownloadFile {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    part def SpeechModelDownloadFile {
        doc /* Describes one downloadable file within a model: its HTTPS source URI, expected SHA-256 checksum, and
             * relative install path beneath the model's install root. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelDownloadFile.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelDownloadFileTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-download-file.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-download-file.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-download-file.yaml");
        }

        attribute sha256Checksum : String;
        attribute relativeInstallPath : String;
    }
}
