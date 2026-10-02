package Speech {
    part def SynthesisEngineBusyException {
        doc /* Exception thrown when ISpeechSynthesizerEngine.CreateSessionAsync is called while
             * this engine's exclusivity lease is already held by another session. Fails fast
             * rather than queueing or awaiting release, since waiting would make this call's
             * latency depend on an unrelated session's teardown with no caller-visible way to
             * bound that wait. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/SynthesisEngineBusyException.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SherpaOnnxSpeechSynthesizerEngineTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer-engine.md (documented as a supporting exception type) */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer-engine.md (documented as a supporting exception type) */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/synthesis-session-lease.yaml */
    }
}
