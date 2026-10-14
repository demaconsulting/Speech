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
}
