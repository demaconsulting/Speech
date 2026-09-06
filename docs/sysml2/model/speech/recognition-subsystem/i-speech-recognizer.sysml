package Speech {
    part def ISpeechRecognizer {
        doc /* Public streaming speech-to-text contract (availability flag, start/stop, recognition-result event) plus its immutable SpeechRecognitionResult/SpeechRecognitionEvent payload types, exposing no inference-engine type. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/ISpeechRecognizer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SherpaOnnxSpeechRecognizerTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/i-speech-recognizer.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/i-speech-recognizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/i-speech-recognizer.yaml */
    }
}