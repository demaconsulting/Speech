package SpeechOnnxNemotronStt {
    part def NemotronRnntGreedyDecoder {
        doc /* RNN-T greedy decoder over the IRnntNetwork seam: blank-terminated symbol search
             * per encoder frame with at most ten symbols per frame, advancing the prediction
             * state per emitted token. Also documents the ONNX-backed NemotronRnntNetwork
             * (decoder and joint graphs). */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/NemotronRnntGreedyDecoder.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/NemotronRnntGreedyDecoderTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-rnnt-greedy-decoder.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-rnnt-greedy-decoder.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-rnnt-greedy-decoder.yaml */
    }
}
