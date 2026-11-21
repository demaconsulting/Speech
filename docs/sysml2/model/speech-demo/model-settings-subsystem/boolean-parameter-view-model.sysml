package SpeechDemo_BooleanParameterViewModel {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_ParameterViewModelBase::*;

    part def BooleanParameterViewModel {
        doc /* Presentation state for one boolean parameter, starting at its declared default. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/ModelSettingsSubsystem/BooleanParameterViewModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/ModelSettingsSubsystem/ParameterViewModelsTests.cs");
            designFiles = ("docs/design/speech-demo/model-settings-subsystem/parameter-view-models.md");
            verificationFiles = ("docs/verification/speech-demo/model-settings-subsystem/parameter-view-models.md");
            requirementFiles = ("docs/reqstream/speech-demo/model-settings-subsystem/parameter-view-models.yaml");
        }

        // Extends the parameter base to show one declared boolean SpeechModelParameter, starting at its
        // default.
        dependency BooleanParameterViewModel to ParameterViewModelBase;
        dependency BooleanParameterViewModel to Speech_SpeechModelParameters::SpeechModelParameters;
        dependency BooleanParameterViewModel to OtsDependencies::CommunityToolkitMvvm;
    }
}
