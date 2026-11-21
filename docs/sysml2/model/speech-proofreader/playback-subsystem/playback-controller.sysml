package SpeechProofreader_PlaybackController {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_ModelSelection::*;
    private import SpeechProofreader_SentenceAudio::*;
    private import SpeechProofreader_SentenceAudioCache::*;
    private import SpeechProofreader_SentencePlayer::*;

    enum def PlaybackState {
        enum Stopped;
        enum Loading;
        enum Playing;
        enum Paused;
    }

    part def PlaybackController {
        doc /* The playback state machine: what is playing (a sentence, a run or an A/B pair), prefetch of the next
             * sentences, and stop, pause and resume. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Playback/PlaybackController.cs");
        }

        // Fetches and prefetches sentence audio from the cache and plays it via the SentencePlayer.
        dependency PlaybackController to ModelSelection;
        dependency PlaybackController to SentenceAudio;
        dependency PlaybackController to SentenceAudioCache;
        dependency PlaybackController to SentencePlayer;
        dependency PlaybackController to Speech_AudioDeviceUnavailableException::AudioDeviceUnavailableException;
        dependency PlaybackController to Speech_SpeechModelContract::SpeechModelContract;
    }
}
