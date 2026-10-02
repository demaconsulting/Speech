package Speech {
    part def RecognitionBackend {
        doc /* Internal mockable seam over one loaded speech-inference backend, together with its
             * factory and their real sherpa-onnx implementations, confining every inference
             * interop call so the recognition pipeline is testable with pure managed fakes. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/IRecognitionBackend.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SherpaOnnxRecognitionEngineTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/sherpa-onnx-recognition-session.md (documented with the pipeline it serves) */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/sherpa-onnx-recognition-session.md (documented with the pipeline it serves) */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/sherpa-onnx-recognition-session.yaml */
    }
}
