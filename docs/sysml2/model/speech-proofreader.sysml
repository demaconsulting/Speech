package SpeechProofreader {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;
    private import SpeechProofreader_App::*;
    private import SpeechProofreader_DiagnosticsSubsystem::*;
    private import SpeechProofreader_ListItems::*;
    private import SpeechProofreader_MainViewModel::*;
    private import SpeechProofreader_PlaybackSubsystem::*;
    private import SpeechProofreader_Program::*;
    private import SpeechProofreader_SessionSubsystem::*;
    private import SpeechProofreader_SettingsSubsystem::*;
    private import SpeechProofreader_SynthesisSubsystem::*;
    private import SpeechProofreader_TextSubsystem::*;

    doc
    /*
     * SpeechProofreader is the non-shipped DemaConsulting.Speech.Proofreader Avalonia desktop tool used to listen to
     * long-form text sentence by sentence with one or two synthesis models (A/B), inspect what the English front end
     * made of each sentence and record flags that feed golden test data. It consumes only the public API of Speech,
     * SpeechTextEn and the synthesis model packages and is never packaged.
     */

    part def SpeechProofreaderSystem {
        doc /* The non-shipped Proofreader desktop tool as a whole (it has no design, verification or requirements
             * documents; its README describes its use). */

        metadata info : SoftwareInfo {
        }

        part avalonia : Avalonia;
        part communityToolkitMvvm : CommunityToolkitMvvm;
        part app : App;
        part program : Program;
        part mainViewModel : MainViewModel;
        part listItems : ListItems;
        part diagnosticsSubsystem : DiagnosticsSubsystem;
        part playbackSubsystem : PlaybackSubsystem;
        part sessionSubsystem : SessionSubsystem;
        part settingsSubsystem : SettingsSubsystem;
        part synthesisSubsystem : SynthesisSubsystem;
        part textSubsystem : TextSubsystem;
    }
}
