package SpeechDemo_SynthesizerSessionFactory {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_ISynthesizerSessionFactory::*;

    part def SynthesizerSessionFactory {
        doc /* Real seam implementation: narrows the public model to ISynthesisModel and forwards it, together with
             * the shared SpeechModelStore and an optional parameterValues bag unchanged, to
             * SpeechSynthesizerFactory.LoadAsync, returning the library's own UnavailableSpeechSynthesizerEngine for
             * a model of the wrong role. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/SynthesisPanelSubsystem/SynthesizerSessionFactory.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/SynthesisPanelSubsystem/SynthesizerSessionFactoryTests.cs");
            designFiles = ("docs/design/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.md");
            verificationFiles = ("docs/verification/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.md");
            requirementFiles = ("docs/reqstream/speech-demo/synthesis-panel-subsystem/synthesizer-session-factory.yaml");
        }

        // Builds synthesizers with SpeechSynthesizerFactory from store-installed models, else an
        // unavailable fallback.
        dependency SynthesizerSessionFactory to ISynthesizerSessionFactory;
        dependency SynthesizerSessionFactory to Speech_ISpeechSynthesizerEngine::ISpeechSynthesizerEngine;
        dependency SynthesizerSessionFactory to Speech_SpeechModelContract::SpeechModelContract;
        dependency SynthesizerSessionFactory to Speech_SpeechModelStore::SpeechModelStore;
        dependency SynthesizerSessionFactory to Speech_SpeechSynthesizerFactory::SpeechSynthesizerFactory;
        dependency SynthesizerSessionFactory to Speech_UnavailableSpeechSynthesizerEngine::UnavailableSpeechSynthesizerEngine;
    }
}
