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
}
