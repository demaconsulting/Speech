package SpeechOnnxNemotronStt {
    part def NemotronEncoder {
        doc /* Cache-aware streaming encoder over the Nemotron encoder graph, exposed through the mockable INemotronEncoder seam; carries the encoder cache tensors between chunks. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/NemotronEncoder.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/NemotronEncoderTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-encoder.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-encoder.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-encoder.yaml */
    }
}
