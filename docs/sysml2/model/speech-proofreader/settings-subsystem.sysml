package SpeechProofreader_SettingsSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_ModelSettingsViewModel::*;
    private import SpeechProofreader_ModelSlotViewModel::*;
    private import SpeechProofreader_ParameterViewModels::*;

    part def SettingsSubsystem {
        doc /* The two model slots: the synthesis-model dropdown with install state and the generic per-model
             * parameter presenters. */

        metadata info : SoftwareInfo {
        }

        part modelSlotViewModel : ModelSlotViewModel;
        part modelSettingsViewModel : ModelSettingsViewModel;
        part parameterViewModels : ParameterViewModels;
    }
}
