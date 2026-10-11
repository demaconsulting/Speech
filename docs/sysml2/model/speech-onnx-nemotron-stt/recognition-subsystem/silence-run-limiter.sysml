package SpeechOnnxNemotronStt {
    part def SilenceRunLimiter {
        doc /* Stateful streaming limiter that truncates quiet runs longer than 400 ms to 400 ms
             * using an adaptive frame-RMS threshold calibrated from the first 400 ms of the
             * stream, preserving all speech samples. Documented together with its companion
             * DitherNoise source (seeded Gaussian dither applied after limiting). */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/SilenceRunLimiter.cs */
        comment sourceRef2 /* Source: src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/DitherNoise.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/SilenceRunLimiterTests.cs */
        comment testRef2 /* Test: test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/DitherNoiseTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-nemotron-stt/recognition-subsystem/silence-run-limiter.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/silence-run-limiter.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/silence-run-limiter.yaml */
    }
}
