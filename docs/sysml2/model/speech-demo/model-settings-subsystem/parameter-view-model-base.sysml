package SpeechDemo_ParameterViewModelBase {
    private import SoftwareItemMetadata::*;

    part def ParameterViewModelBase {
        doc /* Common presentation state shared by every parameter presenter kind: the parameter's id, display name,
             * description, and the boxed current value used to assemble the untyped key-value bag. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/ModelSettingsSubsystem/ParameterViewModelBase.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/ModelSettingsSubsystem/ParameterViewModelsTests.cs");
            designFiles = ("docs/design/speech-demo/model-settings-subsystem/parameter-view-models.md");
            verificationFiles = ("docs/verification/speech-demo/model-settings-subsystem/parameter-view-models.md");
            requirementFiles = ("docs/reqstream/speech-demo/model-settings-subsystem/parameter-view-models.yaml");
        }

        // Shows the shared metadata and value of one SpeechModelParameter as observable MVVM state.
        dependency ParameterViewModelBase to Speech_SpeechModelParameters::SpeechModelParameters;
        dependency ParameterViewModelBase to OtsDependencies::CommunityToolkitMvvm;
    }
}
