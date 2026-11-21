package SpeechProofreader_App {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_MainViewModel::*;
    private import SpeechProofreader_Program::*;
    private import SpeechProofreader_ProofreaderCatalog::*;
    private import SpeechProofreader_SentenceAudioCache::*;
    private import SpeechProofreader_SentenceDiagnostics::*;
    private import SpeechProofreader_SentencePlayer::*;
    private import SpeechProofreader_SentenceSplitter::*;
    private import SpeechProofreader_SynthesisEngineHost::*;
    private import SpeechProofreader_UserSettings::*;

    part def App {
        doc /* The Avalonia application object and the tool's composition root: builds the catalog, the engine host,
             * the audio devices and the main view model. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/App.axaml.cs");
        }

        // Composes the catalog, engine host, audio cache, player, and settings into the MainViewModel.
        dependency App to MainViewModel;
        dependency App to Program;
        dependency App to ProofreaderCatalog;
        dependency App to SentenceAudioCache;
        dependency App to SentenceDiagnostics;
        dependency App to SentencePlayer;
        dependency App to SentenceSplitter;
        dependency App to SynthesisEngineHost;
        dependency App to UserSettings;
        dependency App to Speech_AudioDeviceFactory::AudioDeviceFactory;
        dependency App to OtsDependencies::Avalonia;
    }
}
