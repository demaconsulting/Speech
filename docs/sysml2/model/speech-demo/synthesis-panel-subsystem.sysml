package SpeechDemo {
    part def SynthesisPanelSubsystem {
        doc /* The text-to-speech panel: a demo-owned session seam over the library's
             * SpeechSynthesizerFactory narrowing a public model to the library's synthesis
             * role, the embedded model-settings panel for the selected model's parameters,
             * example Natural Language Audio Tag hints, and the Play/Stop lifecycle with
             * honest reporting of every unavailable state. */

        comment designRef /* Design: docs/design/speech-demo/synthesis-panel-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/synthesis-panel-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/synthesis-panel-subsystem.yaml */

        part iSynthesizerSessionFactory : ISynthesizerSessionFactory;
        part synthesizerSessionFactory : SynthesizerSessionFactory;
        part synthesisPanelViewModel : SynthesisPanelViewModel;
    }

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

    part def SynthesisPanelViewModel {
        doc /* Presentation state for the text-to-speech panel: installed synthesis models,
             * the embedded settings panel for the selected model, example audio tag hints, the
             * async PlayAsync/StopAsync lifecycle driving an ISpeechSynthesizerEngine and an
             * ISynthesisSession, and a CanChangeModel guard that disables model/voice selection
             * while Synthesizing or Playing. SynthesisPlaybackState is derived from
             * SynthesisSessionState via ISynthesisSession.StateChanged rather than ad hoc
             * assignment at each call site. Caches at most one loaded engine (reloaded only when
             * the selected model or the settings panel's built parameter values change) and at
             * most one session created from it (recreated only when the engine was just
             * reloaded or the selected playback device changes), reusing both across many Play
             * calls instead of recomposing them on every click - fixing the bug where a prior
             * design recreated the engine and session on every Play. Forwards
             * Settings.BuildValueBag() to the session factory on an engine reload, so a selected
             * voice genuinely changes synthesized output. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/SynthesisPanelSubsystem/SynthesisPanelViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/SynthesisPanelSubsystem/SynthesisPanelViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/synthesis-panel-subsystem/synthesis-panel-view-model.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/synthesis-panel-subsystem.md (consolidated at subsystem level) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/synthesis-panel-subsystem/synthesis-panel-view-model.yaml */
    }
}
