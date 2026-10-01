package Speech {
    part def SherpaOnnxSpeechRecognizerEngine {
        doc /* Real ISpeechRecognizerEngine implementation: holds one loaded, "hot" IRecognitionBackend
             * and a single-slot exclusivity lease (SemaphoreSlim(1,1), fail-fast, no queueing), and
             * constructs a new SherpaOnnxRecognitionSession bound to one capture device per
             * successful CreateSessionAsync call. Throws RecognitionEngineBusyException rather than
             * blocking when the lease is already held. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/SherpaOnnxSpeechRecognizerEngine.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SherpaOnnxSpeechRecognizerEngineTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/sherpa-onnx-recognition-session.md (documented with the pipeline it serves) */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/sherpa-onnx-recognition-session.md (documented with the pipeline it serves) */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/recognition-session-lease.yaml */
    }
}
