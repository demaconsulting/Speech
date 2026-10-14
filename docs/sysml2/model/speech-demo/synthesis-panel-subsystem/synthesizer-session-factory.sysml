package SpeechDemo {
    part def SynthesizerSessionFactory {
        doc /* Real seam implementation: narrows the public model to ISynthesisModel and
             * forwards it, together with the shared SpeechModelStore and an optional
             * parameterValues bag unchanged, to SpeechSynthesizerFactory.LoadAsync, returning
             * the library's own UnavailableSpeechSynthesizerEngine for a model of the wrong
             * role. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/SynthesisPanelSubsystem/SynthesizerSessionFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/SynthesisPanelSubsystem/SynthesizerSessionFactoryTests.cs */
        comment designRef /* Design: docs/design/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.yaml */
    }
}
