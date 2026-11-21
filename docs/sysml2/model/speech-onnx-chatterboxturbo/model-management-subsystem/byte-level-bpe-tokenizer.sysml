package SpeechOnnxChatterboxTurbo_ByteLevelBpeTokenizer {
    private import SoftwareItemMetadata::*;

    part def ByteLevelBpeTokenizer {
        doc /* Model-family-neutral byte-level BPE tokenizer loaded from an embedded copy of the upstream
             * tokenizer.json, reproducing the reference tokenizer's ids exactly, including atomic added tokens and
             * trailing end-of-text ids. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/ModelManagementSubsystem/ByteLevelBpeTokenizer.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/ModelManagementSubsystem/ByteLevelBpeTokenizerTests.cs");
            designFiles = ("docs/design/speech-onnx-chatterboxturbo/model-management-subsystem/byte-level-bpe-tokenizer.md");
            verificationFiles = ("docs/verification/speech-onnx-chatterboxturbo/model-management-subsystem/byte-level-bpe-tokenizer.md");
            requirementFiles = ("docs/reqstream/speech-onnx-chatterboxturbo/model-management-subsystem/byte-level-bpe-tokenizer.yaml");
        }
    }
}
