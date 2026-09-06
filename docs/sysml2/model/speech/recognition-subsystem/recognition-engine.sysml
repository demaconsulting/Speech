package Speech {
    part def RecognitionEngine {
        doc /* Internal mockable seam over one loaded speech-inference engine, together with its
             * factory and their real sherpa-onnx implementations, confining every inference
             * interop call so the recognition pipeline is testable with pure managed fakes. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/IRecognitionEngine.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SherpaOnnxSpeechRecognizerTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/sherpa-onnx-speech-recognizer.md (documented with the pipeline it serves) */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/sherpa-onnx-speech-recognizer.md (documented with the pipeline it serves) */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/sherpa-onnx-speech-recognizer.yaml */
    }
}
