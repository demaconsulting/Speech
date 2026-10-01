package Speech {
    part def SessionStateChangedEventArgs {
        doc /* Event arguments record carrying the Previous/Current SynthesisSessionState of an
             * ISynthesisSession.StateChanged transition. Declared separately from
             * RecognitionSubsystem's identically-shaped record (which carries
             * RecognitionSessionState instead) rather than shared, matching this library's
             * existing "no cross-subsystem public type" boundary. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/SessionStateChangedEventArgs.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/i-synthesis-session.md (documented as a supporting value type) */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/i-synthesis-session.md (documented as a supporting value type) */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/i-synthesis-session.yaml */
    }
}
