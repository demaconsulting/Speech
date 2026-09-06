package Speech {
    part def SpeechSynthesizerUnavailableException {
        doc /* Exception signaling that an operational member of an unavailable speech
             * synthesizer was invoked, or that a synthesizer which claimed to be available
             * failed on first use. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/SpeechSynthesizerUnavailableException.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/UnavailableSpeechSynthesizerTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem.md (documented as a supporting fallback type) */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem.md (documented as a supporting fallback type) */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/unavailable-speech-synthesizer.yaml */
    }
}
