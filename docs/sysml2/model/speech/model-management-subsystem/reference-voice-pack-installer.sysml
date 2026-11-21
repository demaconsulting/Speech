package Speech_ReferenceVoicePackInstaller {
    private import SoftwareItemMetadata::*;
    private import Speech_ReferenceVoicePack::*;

    enum def EntryKind {
        enum Other;
        enum Voice;
        enum Emotion;
    }

    part def ReferenceVoicePackInstaller {
        doc /* Internal downloader install hook that safely extracts only the voice clips and license/readme files
             * (and, when requested, the per-speaker emotion clips) from a verified reference-voice pack archive,
             * writes pack.json and deletes the archive. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/ReferenceVoicePackInstaller.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/ReferenceVoicePackInstallerTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/reference-voice-pack-installer.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/reference-voice-pack-installer.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/reference-voice-pack-installer.yaml");
        }

        // Safely extracts the files that the voice pack descriptor lists.
        dependency ReferenceVoicePackInstaller to ReferenceVoicePack;
    }
}
