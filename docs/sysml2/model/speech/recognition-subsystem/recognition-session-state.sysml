package Speech {
    part def RecognitionSessionState {
        doc /* Forward-only IRecognitionSession lifecycle enumeration: Created, Starting, Running,
             * Stopping, Stopped, Disposing, Disposed, Faulted. Faulted is reachable from Starting,
             * Running, or Stopping; no other backward or skipped transition is valid. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/RecognitionSessionState.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/i-recognition-session.md (documented as a supporting value type) */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/i-recognition-session.md (documented as a supporting value type) */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/i-recognition-session.yaml */
    }
}
