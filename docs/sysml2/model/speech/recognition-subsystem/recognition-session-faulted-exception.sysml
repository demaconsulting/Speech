package Speech {
    part def RecognitionSessionFaultedException {
        doc /* Exception surfaced from an active IRecognitionSession.GetResultsAsync enumeration
             * when the session transitions to RecognitionSessionState.Faulted, wrapping the
             * underlying cause (for example a capture device becoming unavailable mid-session). */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/RecognitionSessionFaultedException.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionSessionTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/recognition-session-faulted-exception.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/recognition-session-faulted-exception.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/recognition-session-faulted-exception.yaml */
    }
}
