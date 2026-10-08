package Speech {
    part def IRecognitionSession {
        doc /* Layer 5 contract for one streaming recognition session bound to one capture device:
             * a forward-only RecognitionSessionState machine with a StateChanged event,
             * StartAsync/StopAsync lifecycle control, and GetResultsAsync, an async-enumerable,
             * single-consumer stream of ordered provisional and final SpeechRecognitionEvent
             * results. Single-use: a session may not be restarted after Stopped. A session fault
             * surfaces as RecognitionSessionFaultedException from the active GetResultsAsync
             * enumeration. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/IRecognitionSession.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionSessionTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/i-recognition-session.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/i-recognition-session.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/i-recognition-session.yaml */
    }
}
