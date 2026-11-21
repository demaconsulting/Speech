package SpeechProofreader_SentenceAudioCache {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_ModelSelection::*;
    private import SpeechProofreader_SentenceAudio::*;
    private import SpeechProofreader_SynthesisEngineHost::*;

    part def SentenceAudioCache {
        doc /* Remembers synthesized sentences per sentence text, model and settings, and doubles as the prefetch
             * mechanism, retrying after a failure. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Synthesis/SentenceAudioCache.cs");
            testFiles = ("test/DemaConsulting.Speech.Proofreader.Tests/SentenceAudioCacheTests.cs");
        }

        // Asks SynthesisEngineHost for SentenceAudio, keyed by text and ModelSelection.
        dependency SentenceAudioCache to ModelSelection;
        dependency SentenceAudioCache to SentenceAudio;
        dependency SentenceAudioCache to SynthesisEngineHost;
        dependency SentenceAudioCache to Speech_SpeechModelContract::SpeechModelContract;
    }
}
