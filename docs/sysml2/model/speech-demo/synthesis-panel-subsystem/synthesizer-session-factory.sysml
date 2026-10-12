package SpeechDemo {
    part def ISynthesizerSessionFactory {
        doc /* Demo-owned seam accepting the library's public ISpeechModel contract to compose
             * a speech synthesizer engine, so the panel can be tested with a plain fake model
             * with no InternalsVisibleTo grant from the library. Exposes a single LoadAsync
             * member returning an ISpeechSynthesizerEngine, forwarding an optional
             * parameterValues bag (the settings panel's Settings.BuildValueBag()) unchanged;
             * per-run playback-device binding is done later, directly against the returned
             * engine's CreateSessionAsync, so a host can load one engine per model/parameter
             * combination and reuse it across many ISynthesisSession runs. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/SynthesisPanelSubsystem/ISynthesizerSessionFactory.cs */
        comment designRef /* Design: docs/design/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.md (documented together with its sole implementation, SynthesizerSessionFactory) */
        comment verificationRef /* Verification: docs/verification/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.md (documented together with its sole implementation, SynthesizerSessionFactory) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.yaml */
    }

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
