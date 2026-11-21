package SpeechOnnxNemotronStt_RecognitionSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxNemotronStt_DitherNoise::*;
    private import SpeechOnnxNemotronStt_NemotronEncoder::*;
    private import SpeechOnnxNemotronStt_NemotronEngineOptions::*;
    private import SpeechOnnxNemotronStt_NemotronFeatureExtractor::*;
    private import SpeechOnnxNemotronStt_NemotronRnntGreedyDecoder::*;
    private import SpeechOnnxNemotronStt_NemotronRnntNetwork::*;
    private import SpeechOnnxNemotronStt_OnnxNemotronRecognitionEngine::*;
    private import SpeechOnnxNemotronStt_SilenceRunLimiter::*;

    part def RecognitionSubsystem {
        doc /* The real ONNX-Runtime-backed implementation of Speech's public IRecognitionBackend seam over the
             * Nemotron encoder, decoder, and joint graphs, together with the streaming feature extractor, RNN-T
             * greedy decoder, and leading-silence limiter and dither units it composes. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-onnx-nemotron-stt/recognition-subsystem.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/recognition-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem.yaml");
        }

        part onnxNemotronRecognitionEngine : OnnxNemotronRecognitionEngine;
        part nemotronFeatureExtractor : NemotronFeatureExtractor;
        part nemotronEncoder : NemotronEncoder;
        part nemotronRnntNetwork : NemotronRnntNetwork;
        part nemotronRnntGreedyDecoder : NemotronRnntGreedyDecoder;
        part silenceRunLimiter : SilenceRunLimiter;
        part ditherNoise : DitherNoise;
        part nemotronEngineOptions : NemotronEngineOptions;
    }
}
