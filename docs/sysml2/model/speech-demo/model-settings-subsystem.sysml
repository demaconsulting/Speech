package SpeechDemo_ModelSettingsSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_BooleanParameterViewModel::*;
    private import SpeechDemo_ChoiceParameterViewModel::*;
    private import SpeechDemo_ModelSettingsViewModel::*;
    private import SpeechDemo_NumericParameterViewModel::*;
    private import SpeechDemo_ParameterViewModelBase::*;

    part def ModelSettingsSubsystem {
        doc /* The generic per-model tunable-parameter settings panel: presenters for the library's numeric, choice,
             * and boolean parameter kinds, chosen by concrete type alone, plus the honest empty-state shown with no
             * model selected or a model declaring no parameters. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/ModelSettingsSubsystem/ModelSettingsSubsystemTests.cs");
            designFiles = ("docs/design/speech-demo/model-settings-subsystem.md");
            verificationFiles = ("docs/verification/speech-demo/model-settings-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-demo/model-settings-subsystem.yaml");
        }

        part parameterViewModelBase : ParameterViewModelBase;
        part numericParameterViewModel : NumericParameterViewModel;
        part choiceParameterViewModel : ChoiceParameterViewModel;
        part booleanParameterViewModel : BooleanParameterViewModel;
        part modelSettingsViewModel : ModelSettingsViewModel;
    }
}
