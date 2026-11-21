package SpeechOnnxNemotronStt_OnnxNemotronRecognitionEngine {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxNemotronStt_DitherNoise::*;
    private import SpeechOnnxNemotronStt_NemotronEngineOptions::*;
    private import SpeechOnnxNemotronStt_NemotronFeatureExtractor::*;
    private import SpeechOnnxNemotronStt_NemotronVocabulary::*;
    private import SpeechOnnxNemotronStt_SilenceRunLimiter::*;

    part def OnnxNemotronRecognitionEngine {
        doc /* Real IRecognitionBackend implementation running the Nemotron model through ONNX Runtime: limiter,
             * dither, feature extraction, 56-frame chunking with a 9-frame pre-encode cache, cache-aware encoder, and
             * RNN-T greedy decoding, with endpointing on the input clock and on consecutive token-free chunks.
             * Constructed by OnnxNemotronMultilingualRecognitionModel.CreateBackend. Also documents its ONNX
             * collaborators NemotronEncoder (INemotronEncoder) and NemotronRnntNetwork. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/OnnxNemotronRecognitionEngine.cs", "src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/INemotronEncoder.cs", "src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/NemotronEncoder.cs", "src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/IRnntNetwork.cs", "src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/NemotronRnntNetwork.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/OnnxNemotronRecognitionEngineTests.cs");
            designFiles = ("docs/design/speech-onnx-nemotron-stt/recognition-subsystem/onnx-nemotron-recognition-engine.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/onnx-nemotron-recognition-engine.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/onnx-nemotron-recognition-engine.yaml");
        }

        // Runs audio through the limiter, dither, features, and RNN-T decoder, then maps tokens to
        // recognition results.
        dependency OnnxNemotronRecognitionEngine to DitherNoise;
        dependency OnnxNemotronRecognitionEngine to NemotronEngineOptions;
        dependency OnnxNemotronRecognitionEngine to NemotronFeatureExtractor;
        dependency OnnxNemotronRecognitionEngine to SpeechOnnxNemotronStt_NemotronRnntGreedyDecoder::NemotronRnntGreedyDecoder;
        dependency OnnxNemotronRecognitionEngine to NemotronVocabulary;
        dependency OnnxNemotronRecognitionEngine to SilenceRunLimiter;
        dependency OnnxNemotronRecognitionEngine to Speech_RecognitionBackend::RecognitionBackend;
        dependency OnnxNemotronRecognitionEngine to Speech_SpeechRecognitionResult::SpeechRecognitionResult;
    }
}
