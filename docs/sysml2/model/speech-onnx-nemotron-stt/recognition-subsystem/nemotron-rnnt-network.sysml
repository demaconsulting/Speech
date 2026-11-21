package SpeechOnnxNemotronStt_NemotronRnntNetwork {
    private import SoftwareItemMetadata::*;

    part def NemotronRnntNetwork {
        doc /* ONNX-backed decoder and joint networks implementing the IRnntNetwork seam used by the RNN-T greedy
             * decoder. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/NemotronRnntNetwork.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/NemotronRnntNetworkTests.cs");
            designFiles = ("docs/design/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-rnnt-network.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-rnnt-network.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-rnnt-network.yaml");
        }
    }
}
