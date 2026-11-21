package SpeechDemo_ChoiceParameterViewModel {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_ParameterViewModelBase::*;

    part def ChoiceParameterViewModel {
        doc /* Presentation state for one choice parameter, listing its declared options and preselecting the option
             * matching its declared default. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/ModelSettingsSubsystem/ChoiceParameterViewModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/ModelSettingsSubsystem/ParameterViewModelsTests.cs");
            designFiles = ("docs/design/speech-demo/model-settings-subsystem/parameter-view-models.md");
            verificationFiles = ("docs/verification/speech-demo/model-settings-subsystem/parameter-view-models.md");
            requirementFiles = ("docs/reqstream/speech-demo/model-settings-subsystem/parameter-view-models.yaml");
        }

        // Extends the parameter base to show the options of one declared choice SpeechModelParameter.
        dependency ChoiceParameterViewModel to ParameterViewModelBase;
        dependency ChoiceParameterViewModel to Speech_SpeechModelParameters::SpeechModelParameters;
    }
}
