package Speech_SynthesisBackend {
    private import SoftwareItemMetadata::*;
    private import Speech_BackendAudio::*;
    private import Speech_TextAnalysis::*;

    part def SynthesisBackend {
        doc /* Public mockable seam over one loaded speech-synthesis engine, together with its factory and the default
             * factory that asks a model to construct its own backend, confining every inference interop call so the
             * synthesis pipeline is testable with pure managed fakes. Renamed from
             * ISynthesisEngine/ISynthesisEngineFactory so the "engine" vocabulary is reserved for the public Layer 3
             * ISpeechSynthesizerEngine contract; members unchanged. A backend may additionally implement the optional
             * IAnnotatedSynthesisBackend extension to receive a segment's annotations with its text; a backend that
             * does not is never given annotations. Concrete backends are supplied by sibling packages such as
             * SpeechSherpa. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/ISynthesisBackend.cs", "src/DemaConsulting.Speech/SynthesisSubsystem/ISynthesisBackendFactory.cs", "src/DemaConsulting.Speech/SynthesisSubsystem/DefaultSynthesisBackendFactory.cs", "src/DemaConsulting.Speech/SynthesisSubsystem/IAnnotatedSynthesisBackend.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SynthesisSessionTests.cs", "test/DemaConsulting.Speech.Tests/SynthesisSubsystem/DefaultSynthesisBackendFactoryTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/synthesis-backend.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/synthesis-backend.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/synthesis-backend.yaml");
        }

        // Turns a model contract and analyzed text into backend audio.
        dependency SynthesisBackend to BackendAudio;
        dependency SynthesisBackend to Speech_SpeechModelContract::SpeechModelContract;
        dependency SynthesisBackend to TextAnalysis;
    }
}
