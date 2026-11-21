package Speech_ReferenceVoicePack {
    private import SoftwareItemMetadata::*;
    private import Speech_SpeechModelDownloadDescriptor::*;
    private import Speech_SpeechModelDownloadFile::*;
    private import ScalarValues::*;

    enum def ReferenceVoiceSource {
        enum Directory;
        enum User;
        enum Pack;
    }

    part def ReferenceVoicePack {
        doc /* Descriptor of a downloadable reference-voice pack (archive URL, size, SHA-256, license, attribution,
             * measured installed sizes of voices and emotion clips), the built-in voice-zero 1.0 pack, and the
             * ReferenceVoiceInfo (including MoodClips) and ReferenceVoiceSource value types that describe a listed
             * voice. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/ReferenceVoicePack.cs", "src/DemaConsulting.Speech/ModelManagementSubsystem/ReferenceVoiceInfo.cs", "src/DemaConsulting.Speech/ModelManagementSubsystem/ReferenceVoiceSource.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/ReferenceVoicePackTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/reference-voice-pack.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/reference-voice-pack.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/reference-voice-pack.yaml");
        }

        attribute id : String;
        attribute displayName : String;
        attribute uri : String;
        attribute sha256Checksum : String;
        attribute sizeBytes : Integer;
        attribute archiveFileName : String;
        attribute archiveRootFolder : String;
        attribute licenseName : String;
        attribute licenseUrl : String;
        attribute attribution : String;
        attribute sourceUrl : String;
        attribute voicesBytes : Integer;
        attribute emotionsBytes : Integer;

        // Exposes itself as a model download descriptor and plugs in its custom installer.
        dependency ReferenceVoicePack to Speech_ReferenceVoicePackInstaller::ReferenceVoicePackInstaller;
        dependency ReferenceVoicePack to SpeechModelDownloadDescriptor;
        dependency ReferenceVoicePack to SpeechModelDownloadFile;
    }
}
