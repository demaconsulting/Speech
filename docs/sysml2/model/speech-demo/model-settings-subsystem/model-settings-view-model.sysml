package SpeechDemo_ModelSettingsViewModel {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_BooleanParameterViewModel::*;
    private import SpeechDemo_ChoiceParameterViewModel::*;
    private import SpeechDemo_NumericParameterViewModel::*;
    private import SpeechDemo_ParameterViewModelBase::*;

    part def ModelSettingsViewModel {
        doc /* Presentation state for the whole settings panel: rebuilds one presenter per declared parameter whenever
             * the selected model changes, reports the honest empty state with no model or no parameters, assembles
             * the untyped key-value bag, and restores a captured bag onto a replacement model instance. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/ModelSettingsSubsystem/ModelSettingsViewModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/ModelSettingsSubsystem/ModelSettingsViewModelTests.cs");
            designFiles = ("docs/design/speech-demo/model-settings-subsystem/model-settings-view-model.md");
            verificationFiles = ("docs/verification/speech-demo/model-settings-subsystem/model-settings-view-model.md");
            requirementFiles = ("docs/reqstream/speech-demo/model-settings-subsystem/model-settings-view-model.yaml");
        }

        // Reads a model's declared parameters and creates a boolean, choice or numeric presenter for each.
        dependency ModelSettingsViewModel to BooleanParameterViewModel;
        dependency ModelSettingsViewModel to ChoiceParameterViewModel;
        dependency ModelSettingsViewModel to NumericParameterViewModel;
        dependency ModelSettingsViewModel to ParameterViewModelBase;
        dependency ModelSettingsViewModel to Speech_SpeechModelContract::SpeechModelContract;
        dependency ModelSettingsViewModel to Speech_SpeechModelParameters::SpeechModelParameters;
        dependency ModelSettingsViewModel to OtsDependencies::CommunityToolkitMvvm;
    }
}
