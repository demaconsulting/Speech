package SpeechDemo_NumericParameterViewModel {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_ParameterViewModelBase::*;

    part def NumericParameterViewModel {
        doc /* Presentation state for one numeric parameter, starting at its declared default and clamping every
             * written value to its declared range. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/ModelSettingsSubsystem/NumericParameterViewModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/ModelSettingsSubsystem/ParameterViewModelsTests.cs");
            designFiles = ("docs/design/speech-demo/model-settings-subsystem/parameter-view-models.md");
            verificationFiles = ("docs/verification/speech-demo/model-settings-subsystem/parameter-view-models.md");
            requirementFiles = ("docs/reqstream/speech-demo/model-settings-subsystem/parameter-view-models.yaml");
        }

        // Extends the parameter base to show one numeric SpeechModelParameter, clamped to its declared
        // range.
        dependency NumericParameterViewModel to ParameterViewModelBase;
        dependency NumericParameterViewModel to Speech_SpeechModelParameters::SpeechModelParameters;
        dependency NumericParameterViewModel to OtsDependencies::CommunityToolkitMvvm;
    }
}
