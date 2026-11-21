package SpeechProofreader_SynthesisEngineHost {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_ModelSelection::*;
    private import SpeechProofreader_RecordingSynthesisModel::*;
    private import SpeechProofreader_SentenceAudio::*;

    part def SynthesisEngineHost {
        doc /* Loads synthesis engines on demand, serializes synthesis per engine and keeps the last problem for the
             * diagnostics pane. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Synthesis/SynthesisEngineHost.cs");
        }

        // Creates engines via SpeechSynthesizerFactory from the catalog, wrapping them in recorders for
        // diagnostics.
        dependency SynthesisEngineHost to ModelSelection;
        dependency SynthesisEngineHost to RecordingSynthesisModel;
        dependency SynthesisEngineHost to SentenceAudio;
        dependency SynthesisEngineHost to Speech_ISpeechDiagnostics::ISpeechDiagnostics;
        dependency SynthesisEngineHost to Speech_ISpeechSynthesizerEngine::ISpeechSynthesizerEngine;
        dependency SynthesisEngineHost to Speech_SpeechDiagnosticLevel::SpeechDiagnosticLevel;
        dependency SynthesisEngineHost to Speech_SpeechModelCatalog::SpeechModelCatalog;
        dependency SynthesisEngineHost to Speech_SpeechModelContract::SpeechModelContract;
        dependency SynthesisEngineHost to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency SynthesisEngineHost to Speech_SpeechSynthesizerFactory::SpeechSynthesizerFactory;
    }
}
