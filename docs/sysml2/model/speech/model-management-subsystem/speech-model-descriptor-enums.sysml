package Speech_SpeechModelDescriptorEnums {
    private import SoftwareItemMetadata::*;

    enum def SpeechModelRole {
        enum Recognition;
        enum Synthesis;
    }

    enum def SpeechModelState {
        enum NotDownloaded;
        enum Downloading;
        enum Downloaded;
        enum FailedOrCorrupt;
    }

    enum def SpeechModelAudioTagSupport {
        enum None;
        enum ParameterMapped;
        enum Native;
    }

    part def SpeechModelDescriptorEnums {
        doc /* The fixed, small value-set enums a model and the catalog are built around: SpeechModelRole
             * (Recognition/Synthesis), SpeechModelState (NotDownloaded/Downloading/Downloaded/FailedOrCorrupt), and
             * SpeechModelAudioTagSupport (None/ParameterMapped/Native) - a declaration shape only; Layer 2 rendering
             * is done by the SynthesisSubsystem. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelRole.cs", "src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelState.cs", "src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelAudioTagSupport.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelDescriptorEnumsTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-descriptor-enums.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-descriptor-enums.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-descriptor-enums.yaml");
        }
    }
}
