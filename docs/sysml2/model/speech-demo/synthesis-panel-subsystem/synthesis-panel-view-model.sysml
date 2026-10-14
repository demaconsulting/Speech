package SpeechDemo {
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
             * voice genuinely changes synthesized output. Re-reads the catalog on
             * ReferenceVoicesChanged keeping the chosen settings, and shows the voice-cloning
             * consent note and installed pack attribution for Chatterbox Turbo. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/SynthesisPanelSubsystem/SynthesisPanelViewModel.cs, src/DemaConsulting.Speech.Demo/SynthesisPanelSubsystem/SynthesisPlaybackState.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/SynthesisPanelSubsystem/SynthesisPanelViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/synthesis-panel-subsystem/synthesis-panel-view-model.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/synthesis-panel-subsystem/synthesis-panel-view-model.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/synthesis-panel-subsystem/synthesis-panel-view-model.yaml */
    }
}
