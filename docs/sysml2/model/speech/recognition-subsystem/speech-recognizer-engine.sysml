package Speech {
    part def SpeechRecognizerEngine {
        doc /* Real ISpeechRecognizerEngine implementation: holds one loaded, "hot" IRecognitionBackend
             * and a single-slot exclusivity lease (SemaphoreSlim(1,1), fail-fast, no queueing), and
             * constructs a new RecognitionSession bound to one capture device per
             * successful CreateSessionAsync call. Throws RecognitionEngineBusyException rather than
             * blocking when the lease is already held. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/SpeechRecognizerEngine.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SpeechRecognizerEngineTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/speech-recognizer-engine.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/speech-recognizer-engine.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/speech-recognizer-engine.yaml */
    }
}
