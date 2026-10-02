package Speech {
    part def SynthesisSessionState {
        doc /* The lifecycle states an ISynthesisSession passes through: Created, Starting,
             * Running, Stopping, Stopped, Disposing, Disposed, Faulted. Starting/Running/Stopping
             * denote one discrete in-flight SpeakAsync/SynthesizeAsync operation rather than a
             * continuous stream - unlike recognition's continuous capture window, a session
             * returns to Stopped after each operation and is ready to accept another call.
             * Faulted is terminal. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/SynthesisSessionState.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/i-synthesis-session.md (documented as a supporting value type) */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/i-synthesis-session.md (documented as a supporting value type) */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/i-synthesis-session.yaml */
    }
}
