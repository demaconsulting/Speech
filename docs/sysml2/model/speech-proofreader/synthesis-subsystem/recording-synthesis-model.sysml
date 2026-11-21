package SpeechProofreader_RecordingSynthesisModel {
    private import SoftwareItemMetadata::*;

    part def RecordingSynthesisModel {
        doc /* Wraps a synthesis model so the text and annotations each backend call actually received are recorded
             * for the diagnostics pane. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Synthesis/RecordingSynthesisModel.cs");
        }

        // Implements the synthesis contract by delegating to the backend while logging its text and
        // annotations.
        dependency RecordingSynthesisModel to Speech_AudioFormat::AudioFormat;
        dependency RecordingSynthesisModel to Speech_BackendAudio::BackendAudio;
        dependency RecordingSynthesisModel to Speech_Layer2Rendering::Layer2Rendering;
        dependency RecordingSynthesisModel to Speech_SpeechModelContract::SpeechModelContract;
        dependency RecordingSynthesisModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency RecordingSynthesisModel to Speech_SpeechModelDownloadDescriptor::SpeechModelDownloadDescriptor;
        dependency RecordingSynthesisModel to Speech_SpeechModelParameters::SpeechModelParameters;
        dependency RecordingSynthesisModel to Speech_SynthesisBackend::SynthesisBackend;
        dependency RecordingSynthesisModel to Speech_TextAnalysis::TextAnalysis;
    }
}
