package SpeechDemo_ISynthesizerSessionFactory {
    private import SoftwareItemMetadata::*;

    part def ISynthesizerSessionFactory {
        doc /* Demo-owned seam accepting the library's public ISpeechModel contract to compose a speech synthesizer
             * engine, so the panel can be tested with a plain fake model with no InternalsVisibleTo grant from the
             * library. Exposes a single LoadAsync member returning an ISpeechSynthesizerEngine, forwarding an
             * optional parameterValues bag (the settings panel's Settings.BuildValueBag()) unchanged; per-run
             * playback-device binding is done later, directly against the returned engine's CreateSessionAsync, so a
             * host can load one engine per model/parameter combination and reuse it across many ISynthesisSession
             * runs. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/SynthesisPanelSubsystem/ISynthesizerSessionFactory.cs");
            designFiles = ("docs/design/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.md");
            verificationFiles = ("docs/verification/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.md");
            requirementFiles = ("docs/reqstream/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.yaml");
        }

        // Turns a public speech model contract into a synthesizer engine so the panel can use a fake in
        // tests.
        dependency ISynthesizerSessionFactory to Speech_ISpeechSynthesizerEngine::ISpeechSynthesizerEngine;
        dependency ISynthesizerSessionFactory to Speech_SpeechModelContract::SpeechModelContract;
    }
}
