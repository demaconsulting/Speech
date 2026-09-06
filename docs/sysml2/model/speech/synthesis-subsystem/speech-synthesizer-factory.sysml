package Speech {
    part def SpeechSynthesizerFactory {
        doc /* Composition entry point that returns a working speech synthesizer for an
             * installed synthesis model and available playback device, or an honest unavailable
             * fallback, without ever throwing for an ordinary machine state. Forwards an
             * optional, session-level parameterValues bag (for example a selected voice)
             * unchanged to the constructed synthesizer. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/SpeechSynthesizerFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SpeechSynthesizerFactoryTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/speech-synthesizer-factory.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/speech-synthesizer-factory.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/speech-synthesizer-factory.yaml */
    }
}
