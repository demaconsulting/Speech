package Speech {
    part def SpeechSynthesizerEngine {
        doc /* Real ISpeechSynthesizerEngine implementation: holds one loaded SynthesisBackend,
             * enforces single-session exclusivity with a fail-fast binary semaphore lease held
             * for a leased session's entire life through DisposeAsync completion (a concurrent
             * CreateSessionAsync call while the lease is held throws
             * SynthesisEngineBusyException), and constructs SynthesisSession instances
             * bound to a caller-supplied playback device. Disposing the engine disposes any
             * still-active leased session first, then the owned backend. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/SpeechSynthesizerEngine.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SpeechSynthesizerEngineTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/speech-synthesizer-engine.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/speech-synthesizer-engine.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/synthesis-session-lease.yaml */
    }
}
