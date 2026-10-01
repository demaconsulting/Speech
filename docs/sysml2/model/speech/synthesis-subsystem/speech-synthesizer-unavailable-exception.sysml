package Speech {
    part def SpeechSynthesizerUnavailableException {
        doc /* Exception signaling that an operational member of an unavailable
             * ISpeechSynthesizerEngine or ISynthesisSession was invoked, or that a session which
             * claimed to be available faulted and was asked to operate again. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/SpeechSynthesizerUnavailableException.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/UnavailableSpeechSynthesizerEngineTests.cs, UnavailableSynthesisSessionTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem.md (documented as a supporting fallback type) */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem.md (documented as a supporting fallback type) */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/unavailable-speech-synthesizer-engine.yaml, unavailable-synthesis-session.yaml */
    }
}
