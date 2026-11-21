package SpeechProofreader_PlaybackSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_PlaybackController::*;
    private import SpeechProofreader_SentencePlayer::*;

    part def PlaybackSubsystem {
        doc /* Sentence playback: the controller of what is playing and the player that converts and plays one
             * sentence's audio. */

        metadata info : SoftwareInfo {
        }

        part playbackController : PlaybackController;
        part sentencePlayer : SentencePlayer;
    }
}
