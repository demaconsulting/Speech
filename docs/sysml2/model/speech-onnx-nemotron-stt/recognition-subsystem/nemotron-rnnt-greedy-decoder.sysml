package SpeechOnnxNemotronStt_NemotronRnntGreedyDecoder {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxNemotronStt_OnnxNemotronRecognitionEngine::*;

    part def NemotronRnntGreedyDecoder {
        doc /* RNN-T greedy decoder over the IRnntNetwork seam: blank-terminated symbol search per encoder frame with
             * at most ten symbols per frame, advancing the prediction state per emitted token. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/NemotronRnntGreedyDecoder.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/NemotronRnntGreedyDecoderTests.cs");
            designFiles = ("docs/design/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-rnnt-greedy-decoder.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-rnnt-greedy-decoder.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-rnnt-greedy-decoder.yaml");
        }

        // Decodes the recognition engine's encoder frames into tokens using greedy RNN-T search.
        dependency NemotronRnntGreedyDecoder to OnnxNemotronRecognitionEngine;
    }
}
