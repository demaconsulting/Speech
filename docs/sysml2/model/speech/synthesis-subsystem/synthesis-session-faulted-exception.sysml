package Speech {
    part def SynthesisSessionFaultedException {
        doc /* Exception thrown when an operation is attempted on, or surfaced from, an
             * ISynthesisSession that has transitioned to SynthesisSessionState.Faulted (an
             * in-flight SpeakAsync/SynthesizeAsync operation failed for a reason other than its
             * own requested cancellation, including a native call abandoned without ever being
             * requested to stop). Carries the original fault as its inner exception; the session
             * is terminal and must be disposed and replaced. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/SynthesisSessionFaultedException.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SherpaOnnxSynthesisSessionTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/sherpa-onnx-synthesis-session.md (documented as a supporting exception type) */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/sherpa-onnx-synthesis-session.md (documented as a supporting exception type) */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/sherpa-onnx-synthesis-session.yaml */
    }
}
