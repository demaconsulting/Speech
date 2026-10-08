package SpeechSherpa {
    part def RecognitionSubsystem {
        doc /* The real sherpa-onnx implementation of Speech's public IRecognitionBackend
             * seam over sherpa-onnx's streaming recognizer. */

        comment designRef /* Design: docs/design/speech-sherpa/recognition-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-sherpa/recognition-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-sherpa/recognition-subsystem.yaml */

        part sherpaOnnxRecognitionEngine : SherpaOnnxRecognitionEngine;
    }
}
