package Speech {
    part def SherpaOnnxSpeechRecognizer {
        doc /* Real streaming recognition pipeline: enqueues captured audio off the audio callback thread, converts it to the model required format, feeds it to the recognition engine, normalizes each result's text via the owning IRecognitionModel.NormalizeText, and raises ordered provisional and final results. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/SherpaOnnxSpeechRecognizer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SherpaOnnxSpeechRecognizerTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/sherpa-onnx-speech-recognizer.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/sherpa-onnx-speech-recognizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/sherpa-onnx-speech-recognizer.yaml */
    }
}