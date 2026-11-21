package SpeechProofreader_MainViewModel {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_FlagCategory::*;
    private import SpeechProofreader_ListItems::*;
    private import SpeechProofreader_MarkdownStripper::*;
    private import SpeechProofreader_ModelSelection::*;
    private import SpeechProofreader_ModelSlotViewModel::*;
    private import SpeechProofreader_PlaybackController::*;
    private import SpeechProofreader_ProofreadSentence::*;
    private import SpeechProofreader_ProofreadSession::*;
    private import SpeechProofreader_ProofreaderCatalog::*;
    private import SpeechProofreader_SentenceAudio::*;
    private import SpeechProofreader_SentenceAudioCache::*;
    private import SpeechProofreader_SentenceDiagnostics::*;
    private import SpeechProofreader_SentencePlayer::*;
    private import SpeechProofreader_SentenceSplitter::*;
    private import SpeechProofreader_SessionFile::*;
    private import SpeechProofreader_SynthesisEngineHost::*;
    private import SpeechProofreader_TsvExporter::*;
    private import SpeechProofreader_UserSettings::*;

    part def MainViewModel {
        doc /* The single view model of the Proofreader window: the text and its sentences, playback, the two model
             * slots for A/B listening, diagnostics, and (in its partial files) flag creation and editing and session
             * save, load and TSV export. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/MainViewModel.cs", "tools/DemaConsulting.Speech.Proofreader/MainViewModel.Flags.cs", "tools/DemaConsulting.Speech.Proofreader/MainViewModel.Session.cs");
            testFiles = ("test/DemaConsulting.Speech.Proofreader.Tests/MainViewModelFlagTests.cs", "test/DemaConsulting.Speech.Proofreader.Tests/MainViewModelPlaybackMarksTests.cs", "test/DemaConsulting.Speech.Proofreader.Tests/MainViewModelSessionTests.cs", "test/DemaConsulting.Speech.Proofreader.Tests/MainViewModelUsabilityTests.cs");
        }

        // Coordinates sentences, playback, model slots, diagnostics, and session/TSV files over the engine
        // host.
        dependency MainViewModel to FlagCategory;
        dependency MainViewModel to ListItems;
        dependency MainViewModel to MarkdownStripper;
        dependency MainViewModel to ModelSelection;
        dependency MainViewModel to ModelSlotViewModel;
        dependency MainViewModel to PlaybackController;
        dependency MainViewModel to ProofreadSentence;
        dependency MainViewModel to ProofreadSession;
        dependency MainViewModel to ProofreaderCatalog;
        dependency MainViewModel to SentenceAudio;
        dependency MainViewModel to SentenceAudioCache;
        dependency MainViewModel to SentenceDiagnostics;
        dependency MainViewModel to SentencePlayer;
        dependency MainViewModel to SentenceSplitter;
        dependency MainViewModel to SessionFile;
        dependency MainViewModel to SynthesisEngineHost;
        dependency MainViewModel to TsvExporter;
        dependency MainViewModel to UserSettings;
        dependency MainViewModel to SpeechOnnxKokoro_OnnxKokoroEnglishSynthesisModel::OnnxKokoroEnglishSynthesisModel;
        dependency MainViewModel to SpeechOnnxPiper_OnnxPiperLibriTtsEnSynthesisModel::OnnxPiperLibriTtsEnSynthesisModel;
        dependency MainViewModel to Speech_AudioDeviceInUseException::AudioDeviceInUseException;
        dependency MainViewModel to Speech_SpeechModelContract::SpeechModelContract;
        dependency MainViewModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency MainViewModel to OtsDependencies::CommunityToolkitMvvm;
    }
}
