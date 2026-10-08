package Speech {
    part def RecognitionEngineBusyException {
        doc /* Exception signaling that ISpeechRecognizerEngine.CreateSessionAsync was called while
             * the engine's single lease is already held by another live IRecognitionSession. The
             * engine fails fast (no queueing) rather than blocking the caller. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/RecognitionEngineBusyException.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SpeechRecognizerEngineTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/i-speech-recognizer-engine.md (documented as a supporting fault type) */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/i-speech-recognizer-engine.md (documented as a supporting fault type) */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/recognition-session-lease.yaml */
    }
}
