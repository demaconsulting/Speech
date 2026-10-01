package Speech {
    part def UnavailableRecognitionSession {
        doc /* Honest "unavailable" IRecognitionSession fallback returned by
             * UnavailableSpeechRecognizerEngine.CreateSessionAsync: reports IsAvailable as false
             * and State as Faulted, and throws SpeechRecognizerUnavailableException only when an
             * operational member is invoked. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/UnavailableRecognitionSession.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/UnavailableRecognitionSessionTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/unavailable-recognition-session.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/unavailable-recognition-session.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/unavailable-recognition-session.yaml */
    }
}
