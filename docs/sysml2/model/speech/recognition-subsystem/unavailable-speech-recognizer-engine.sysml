package Speech {
    part def UnavailableSpeechRecognizerEngine {
        doc /* Honest "unavailable" ISpeechRecognizerEngine fallback: reports IsAvailable as false
             * and CreateSessionAsync returns UnavailableRecognitionSession.Instance rather than
             * throwing for an ordinary unavailable machine state. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/UnavailableSpeechRecognizerEngine.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/UnavailableSpeechRecognizerEngineTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/unavailable-speech-recognizer-engine.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/unavailable-speech-recognizer-engine.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/unavailable-speech-recognizer-engine.yaml */
    }
}
