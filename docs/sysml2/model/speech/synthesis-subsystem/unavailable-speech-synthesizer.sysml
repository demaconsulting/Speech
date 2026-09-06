package Speech {
    part def UnavailableSpeechSynthesizer {
        doc /* Honest "unavailable" ISpeechSynthesizer fallback: reports IsAvailable as false and
             * throws SpeechSynthesizerUnavailableException only when an operational member is
             * invoked. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/UnavailableSpeechSynthesizer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/UnavailableSpeechSynthesizerTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/unavailable-speech-synthesizer.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/unavailable-speech-synthesizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/unavailable-speech-synthesizer.yaml */
    }
}
