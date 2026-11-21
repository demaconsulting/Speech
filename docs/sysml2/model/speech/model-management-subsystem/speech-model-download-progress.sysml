package Speech_SpeechModelDownloadProgress {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    part def SpeechModelDownloadProgress {
        doc /* Reports progress of a single file transfer within a model download - file index, file count, bytes
             * transferred, and optional total bytes - suitable for IProgress<T> consumption by a GUI progress bar. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelDownloadProgress.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelDownloadProgressTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-download-progress.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-download-progress.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-download-progress.yaml");
        }

        attribute fileIndex : Integer;
        attribute fileCount : Integer;
        attribute bytesTransferred : Integer;
        attribute totalBytes : Integer [0..1];
        attribute fractionComplete : Real [0..1];
    }
}
