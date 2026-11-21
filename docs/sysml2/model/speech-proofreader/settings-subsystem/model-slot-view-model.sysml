package SpeechProofreader_ModelSlotViewModel {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_ModelSelection::*;
    private import SpeechProofreader_ModelSettingsViewModel::*;
    private import SpeechProofreader_PlaybackController::*;
    private import SpeechProofreader_ProofreadSession::*;

    part def ModelSlotViewModel {
        doc /* One model slot: the dropdown of registered synthesis models with their install state, the selected
             * model and its settings. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Settings/ModelSlotViewModel.cs");
        }

        // Lists catalog model descriptors and install states, and passes the selection to settings and
        // playback.
        dependency ModelSlotViewModel to ModelSelection;
        dependency ModelSlotViewModel to ModelSettingsViewModel;
        dependency ModelSlotViewModel to PlaybackController;
        dependency ModelSlotViewModel to ProofreadSession;
        dependency ModelSlotViewModel to Speech_SpeechModelContract::SpeechModelContract;
        dependency ModelSlotViewModel to Speech_SpeechModelDescriptor::SpeechModelDescriptor;
        dependency ModelSlotViewModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency ModelSlotViewModel to OtsDependencies::CommunityToolkitMvvm;
    }
}
