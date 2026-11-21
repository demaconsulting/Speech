package Speech_SpeechModelDescriptor {
    private import SoftwareItemMetadata::*;
    private import Speech_SpeechModelContract::*;
    private import Speech_SpeechModelDescriptorEnums::*;

    part def SpeechModelDescriptor {
        doc /* Immutable catalog read-model pairing one ISpeechModel with its current SpeechModelState, plus
             * Id/DisplayName/Role passthrough properties. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelDescriptor.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelDescriptorTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-descriptor.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-descriptor.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-descriptor.yaml");
        }

        // Pairs a model contract with its role and state enumerations.
        dependency SpeechModelDescriptor to SpeechModelContract;
        dependency SpeechModelDescriptor to SpeechModelDescriptorEnums;
    }
}
