package SpeechOnnxNemotronStt {
    part def DitherNoise {
        doc /* Seeded Gaussian dither source applied to the limited audio so digital silence does
             * not produce an exactly-zero log-mel input. The same seed yields the same sequence,
             * the sequence can be restarted, and zero amplitude leaves samples unchanged. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/DitherNoise.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/DitherNoiseTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-nemotron-stt/recognition-subsystem/dither-noise.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/dither-noise.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/dither-noise.yaml */
    }
}
