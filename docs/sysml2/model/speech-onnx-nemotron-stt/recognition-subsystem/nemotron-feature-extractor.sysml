package SpeechOnnxNemotronStt {
    part def NemotronFeatureExtractor {
        doc /* Streaming 128-band log-mel front end for the Nemotron encoder: 0.97 preemphasis,
             * centered reflect padding, 512-point FFT of a 400-sample periodic Hann window, hop
             * of 160 samples, Slaney mel filters. Streaming output equals one-shot output. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/NemotronFeatureExtractor.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/NemotronFeatureExtractorTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-feature-extractor.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-feature-extractor.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-feature-extractor.yaml */
    }
}
