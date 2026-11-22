package SpeechSttEvaluation_EngineOptionOverrides {
    private import SoftwareItemMetadata::*;

    part def EngineOptionOverrides {
        doc /* The --engine-option Name=Value settings of an evaluation run and the backend they produce: a declared
             * public parameter of the model (such as language), converted to the parameter's type, or for the ONNX
             * Nemotron model only an internal engine option (BlankPenalty, FlushTailChunks, TrailingSilenceMs,
             * TrailingSilenceAfterSpeechMs, MaxUtteranceMs) passed through the model's internal CreateBackend overload.
             * An unknown name or an engine option for another model is an error. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.SttEvaluation/EngineOptionOverrides.cs");
        }

        // Creates the backend through the public model contract or, for engine options, through the ONNX Nemotron
        // model's internal options overload.
        dependency EngineOptionOverrides to SpeechOnnxNemotronStt_OnnxNemotronMultilingualRecognitionModel::OnnxNemotronMultilingualRecognitionModel;
        dependency EngineOptionOverrides to SpeechOnnxNemotronStt_NemotronEngineOptions::NemotronEngineOptions;
        dependency EngineOptionOverrides to SpeechOnnxNemotronStt_NemotronEndpointDetector::NemotronEndpointDetector;
        dependency EngineOptionOverrides to Speech_RecognitionBackend::RecognitionBackend;
        dependency EngineOptionOverrides to Speech_SpeechModelContract::SpeechModelContract;
        dependency EngineOptionOverrides to Speech_SpeechModelParameters::SpeechModelParameters;
    }
}