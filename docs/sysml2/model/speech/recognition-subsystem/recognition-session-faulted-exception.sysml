package Speech {
    part def RecognitionSessionFaultedException {
        doc /* Exception surfaced from an active IRecognitionSession.GetResultsAsync enumeration
             * when the session transitions to RecognitionSessionState.Faulted, wrapping the
             * underlying cause (for example a capture device becoming unavailable mid-session). */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/RecognitionSessionFaultedException.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SherpaOnnxRecognitionSessionTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/i-recognition-session.md (documented as a supporting fault type) */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/i-recognition-session.md (documented as a supporting fault type) */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/i-recognition-session.yaml */
    }
}
