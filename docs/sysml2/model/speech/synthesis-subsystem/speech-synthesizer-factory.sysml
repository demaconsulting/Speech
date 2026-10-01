package Speech {
    part def SpeechSynthesizerFactory {
        doc /* Composition entry point that asynchronously loads an installed synthesis model
             * into a working ISpeechSynthesizerEngine, or returns an honest
             * UnavailableSpeechSynthesizerEngine fallback, without ever throwing for an ordinary
             * machine state. No longer takes a playback device parameter - a device is bound
             * later, per session, via ISpeechSynthesizerEngine.CreateSessionAsync, so the
             * model-load step and the device-bind step can fail and be retried independently.
             * Forwards an optional, engine-level parameterValues bag (for example a selected
             * voice) unchanged to the constructed engine. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/SpeechSynthesizerFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SpeechSynthesizerFactoryTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/speech-synthesizer-factory.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/speech-synthesizer-factory.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/speech-synthesizer-factory.yaml */
    }
}
