package Speech {
    part def UnavailableSynthesisSession {
        doc /* Honest "unavailable" ISynthesisSession fallback: reports IsAvailable as false,
             * always reports SynthesisSessionState.Created (it never transitions), and throws
             * SpeechSynthesizerUnavailableException only when SpeakAsync or SynthesizeAsync is
             * invoked; StopAsync and DisposeAsync are safe no-ops. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/UnavailableSynthesisSession.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/UnavailableSynthesisSessionTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/unavailable-synthesis-session.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/unavailable-synthesis-session.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/unavailable-synthesis-session.yaml */
    }
}
