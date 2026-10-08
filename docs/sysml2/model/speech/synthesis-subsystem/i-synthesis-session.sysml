package Speech {
    part def ISynthesisSession {
        doc /* Layer 5: a cheap text-to-speech session, bound to exactly one playback device
             * instance for its entire life, obtained from
             * ISpeechSynthesizerEngine.CreateSessionAsync. Exposes an availability flag, a
             * SynthesisSessionState lifecycle state with a StateChanged event, non-overlapping
             * SpeakAsync/SynthesizeAsync operations (a second call while one is in flight throws
             * InvalidOperationException), and StopAsync to request cooperative cancellation of
             * whichever operation is currently in flight. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/ISynthesisSession.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SynthesisSessionTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/i-synthesis-session.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/i-synthesis-session.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/i-synthesis-session.yaml */
    }
}
