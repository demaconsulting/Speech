package SpeechOnnxNemotronStt_NemotronEncoder {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxNemotronStt_NemotronEngineOptions::*;
    private import SpeechOnnxNemotronStt_NemotronFeatureExtractor::*;
    private import SpeechOnnxNemotronStt_OnnxNemotronRecognitionEngine::*;

    part def NemotronEncoder {
        doc /* Cache-aware streaming encoder over the Nemotron encoder graph, exposed through the mockable
             * INemotronEncoder seam; carries the encoder cache tensors between chunks. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/NemotronEncoder.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/NemotronEncoderTests.cs");
            designFiles = ("docs/design/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-encoder.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-encoder.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-encoder.yaml");
        }

        // Runs the ONNX encoder on extracted features chunk by chunk, using the engine options.
        dependency NemotronEncoder to NemotronEngineOptions;
        dependency NemotronEncoder to NemotronFeatureExtractor;
        dependency NemotronEncoder to OnnxNemotronRecognitionEngine;
        dependency NemotronEncoder to OtsDependencies::MicrosoftMlOnnxRuntime;
    }
}
