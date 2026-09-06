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
             * a speech synthesizer, so the panel can be tested with a plain fake model with no
             * InternalsVisibleTo grant from the library. Forwards an optional parameterValues
             * bag (the settings panel's Settings.BuildValueBag()) unchanged. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/SynthesisPanelSubsystem/ISynthesizerSessionFactory.cs */
        comment designRef /* Design: docs/design/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.md (documented together with its sole implementation, SynthesizerSessionFactory) */
        comment verificationRef /* Verification: docs/verification/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.md (documented together with its sole implementation, SynthesizerSessionFactory) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.yaml */
    }

    part def SynthesizerSessionFactory {
        doc /* Real seam implementation: resolves the model's installed-files directory from
             * the shared SpeechModelStore, narrows the public model to ISynthesisModel, and
             * forwards to SpeechSynthesizerFactory.Create (including an optional parameterValues
             * bag unchanged), returning the library's own UnavailableSpeechSynthesizer for a
             * model of the wrong role. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/SynthesisPanelSubsystem/SynthesizerSessionFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/SynthesisPanelSubsystem/SynthesizerSessionFactoryTests.cs */
        comment designRef /* Design: docs/design/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.yaml */
    }

    part def SynthesisPanelViewModel {
        doc /* Presentation state for the text-to-speech panel: installed synthesis models,
             * the embedded settings panel for the selected model, example audio tag hints, the
             * Play/Stop lifecycle with honest unavailable-state reporting, and a CanChangeModel
             * guard that disables model/voice selection while Synthesizing or Playing. Forwards
             * Settings.BuildValueBag() to the session factory on Play, so a selected voice
             * genuinely changes synthesized output. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/SynthesisPanelSubsystem/SynthesisPanelViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/SynthesisPanelSubsystem/SynthesisPanelViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/synthesis-panel-subsystem/synthesis-panel-view-model.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/synthesis-panel-subsystem/synthesis-panel-view-model.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/synthesis-panel-subsystem/synthesis-panel-view-model.yaml */
    }
}
