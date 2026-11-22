package SpeechOnnxNemotronStt_NemotronRnntNetwork {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxNemotronStt_NemotronEncoder::*;
    private import SpeechOnnxNemotronStt_OnnxNemotronRecognitionEngine::*;

    part def NemotronRnntNetwork {
        doc /* ONNX-backed decoder and joint networks implementing the IRnntNetwork seam used by the RNN-T greedy
             * decoder. Greedy selection subtracts a configurable blank penalty from the blank logit, validated finite
             * and not negative before the sessions are taken over; the default 1.0 was chosen by a sweep from 0 to
             * 3 with the evaluation tool. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/NemotronRnntNetwork.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/NemotronRnntNetworkTests.cs");
            designFiles = ("docs/design/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-rnnt-network.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-rnnt-network.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-rnnt-network.yaml");
        }

        // Runs the ONNX Runtime decoder and joint networks that the recognition engine's greedy decoder
        // calls.
        dependency NemotronRnntNetwork to NemotronEncoder;
        dependency NemotronRnntNetwork to OnnxNemotronRecognitionEngine;
        dependency NemotronRnntNetwork to OtsDependencies::MicrosoftMlOnnxRuntime;
    }
}
