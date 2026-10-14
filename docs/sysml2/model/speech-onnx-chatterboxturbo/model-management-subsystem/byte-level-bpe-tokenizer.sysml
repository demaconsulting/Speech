package SpeechOnnxChatterboxTurbo {
    part def ByteLevelBpeTokenizer {
        doc /* Model-family-neutral byte-level BPE tokenizer loaded from an embedded copy of the
             * upstream tokenizer.json, reproducing the reference tokenizer's ids exactly,
             * including atomic added tokens and trailing end-of-text ids. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/ModelManagementSubsystem/ByteLevelBpeTokenizer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/ModelManagementSubsystem/ByteLevelBpeTokenizerTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-chatterboxturbo/model-management-subsystem/byte-level-bpe-tokenizer.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-chatterboxturbo/model-management-subsystem/byte-level-bpe-tokenizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-chatterboxturbo/model-management-subsystem/byte-level-bpe-tokenizer.yaml */
    }
}
