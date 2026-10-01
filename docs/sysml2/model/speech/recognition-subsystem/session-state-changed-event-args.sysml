package Speech {
    part def SessionStateChangedEventArgs {
        doc /* Immutable record payload for IRecognitionSession.StateChanged, carrying the
             * Previous and Current RecognitionSessionState values of one transition. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/SessionStateChangedEventArgs.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/i-recognition-session.md (documented as a supporting value type) */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/i-recognition-session.md (documented as a supporting value type) */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/i-recognition-session.yaml */
    }
}
