package SpeechProofreader_SynthesisSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_ModelSelection::*;
    private import SpeechProofreader_ProofreaderCatalog::*;
    private import SpeechProofreader_RecordingSynthesisModel::*;
    private import SpeechProofreader_SentenceAudio::*;
    private import SpeechProofreader_SentenceAudioCache::*;
    private import SpeechProofreader_SynthesisEngineHost::*;

    part def SynthesisSubsystem {
        doc /* Synthesis for the listener: the model catalog, engines loaded on demand, the per-sentence audio cache
             * and the recording of what each backend call received. */

        metadata info : SoftwareInfo {
        }

        part proofreaderCatalog : ProofreaderCatalog;
        part modelSelection : ModelSelection;
        part synthesisEngineHost : SynthesisEngineHost;
        part sentenceAudio : SentenceAudio;
        part sentenceAudioCache : SentenceAudioCache;
        part recordingSynthesisModel : RecordingSynthesisModel;
    }
}
