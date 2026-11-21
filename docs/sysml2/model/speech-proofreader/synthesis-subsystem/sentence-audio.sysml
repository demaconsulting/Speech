package SpeechProofreader_SentenceAudio {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_RecordingSynthesisModel::*;
    private import ScalarValues::*;

    part def SentenceAudio {
        doc /* The result of synthesizing one sentence with one model configuration. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Synthesis/SentenceAudio.cs");
        }

        attribute error : String [0..1];

        // Holds the engine output and the RecordingSynthesisModel captures for one synthesized sentence.
        dependency SentenceAudio to RecordingSynthesisModel;
        dependency SentenceAudio to Speech_ISpeechSynthesizerEngine::ISpeechSynthesizerEngine;
    }
}
