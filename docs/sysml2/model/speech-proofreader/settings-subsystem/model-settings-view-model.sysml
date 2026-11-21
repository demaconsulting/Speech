package SpeechProofreader_ModelSettingsViewModel {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_ParameterViewModels::*;

    part def ModelSettingsViewModel {
        doc /* Presentation state for the tunable parameters of the model a slot has selected. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Settings/ModelSettingsViewModel.cs");
        }

        // Turns the selected model's SpeechModelParameters into ParameterViewModels for editing.
        dependency ModelSettingsViewModel to ParameterViewModels;
        dependency ModelSettingsViewModel to Speech_SpeechModelContract::SpeechModelContract;
        dependency ModelSettingsViewModel to Speech_SpeechModelParameters::SpeechModelParameters;
        dependency ModelSettingsViewModel to OtsDependencies::CommunityToolkitMvvm;
    }
}
