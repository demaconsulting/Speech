package Speech {
    part def UnavailableSpeechRecognizer {
        doc /* Honest "unavailable" ISpeechRecognizer fallback: reports IsAvailable as false and throws SpeechRecognizerUnavailableException only when an operational member is invoked. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/UnavailableSpeechRecognizer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/UnavailableSpeechRecognizerTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/unavailable-speech-recognizer.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/unavailable-speech-recognizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/unavailable-speech-recognizer.yaml */
    }
}