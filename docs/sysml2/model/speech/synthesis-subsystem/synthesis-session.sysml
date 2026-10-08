package Speech {
    part def SynthesisSession {
        doc /* Real ISynthesisSession implementation: normalizes and tag-parses input text, Layer
             * 2 renders it into an ordered SpeechPlan, synthesizes each segment on a dedicated
             * worker thread (bounding how long a non-cooperative native call is waited on before
             * being abandoned), and - for SpeakAsync - plays the resulting audio through this
             * session's bound playback device while waiting for genuine playback drain before
             * stopping it. An explicit SynthesisSessionState state machine and an overlap guard
             * (at most one SpeakAsync/SynthesizeAsync call in flight at a time) wrap this
             * pipeline; StopAsync cancels an in-flight operation deterministically and a failed
             * operation transitions the session to the terminal Faulted state. Resolves the
             * speaker id passed to each segment's Generate call from the constructor-supplied
             * parameterValues bag via ISynthesisModel.ResolveSpeakerId once per segment,
             * independent of the per-segment ParameterOverrides speed/volume mechanism. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/SynthesisSession.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SynthesisSessionTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/synthesis-session.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/synthesis-session.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/synthesis-session.yaml */
    }
}
