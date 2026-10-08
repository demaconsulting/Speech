package SpeechSherpa {
    part def SherpaOnnxRecognitionEngine {
        doc /* Real IRecognitionBackend implementation wrapping one loaded sherpa-onnx
             * streaming recognizer, confining every recognition inference interop call behind
             * Speech's public mockable seam. Constructed by the recognition models'
             * CreateBackend overrides. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Sherpa/RecognitionSubsystem/SherpaOnnxRecognitionEngine.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Sherpa.Tests/RecognitionSubsystem/SherpaOnnxRecognitionEngineTests.cs */
        comment designRef /* Design: docs/design/speech-sherpa/recognition-subsystem/sherpa-onnx-recognition-engine.md */
        comment verificationRef /* Verification: docs/verification/speech-sherpa/recognition-subsystem/sherpa-onnx-recognition-engine.md */
        comment reqRef /* Requirements: docs/reqstream/speech-sherpa/recognition-subsystem/sherpa-onnx-recognition-engine.yaml */
    }
}
