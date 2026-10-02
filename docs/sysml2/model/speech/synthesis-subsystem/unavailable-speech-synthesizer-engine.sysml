package Speech {
    part def UnavailableSpeechSynthesizerEngine {
        doc /* Honest "unavailable" ISpeechSynthesizerEngine fallback: reports IsAvailable as
             * false; CreateSessionAsync always succeeds, returning the shared
             * UnavailableSynthesisSession instance, since binding a device to an already
             * unavailable engine is itself an ordinary (if useless) composition, not an error -
             * only the returned session's operational members throw
             * SpeechSynthesizerUnavailableException. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/UnavailableSpeechSynthesizerEngine.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/UnavailableSpeechSynthesizerEngineTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/unavailable-speech-synthesizer-engine.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/unavailable-speech-synthesizer-engine.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/unavailable-speech-synthesizer-engine.yaml */
    }
}
