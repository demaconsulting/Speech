package SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboSynthesisEngine {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxChatterboxTurbo_ByteLevelBpeTokenizer::*;
    private import SpeechOnnxChatterboxTurbo_ChatterboxTurboCapabilityProfile::*;
    private import SpeechOnnxChatterboxTurbo_ChatterboxTurboConditioning::*;
    private import SpeechOnnxChatterboxTurbo_ChatterboxTurboReferenceVoice::*;

    part def OnnxChatterboxTurboSynthesisEngine {
        doc /* The ISynthesisBackend implementation: prompt assembly, bounded autoregressive generation, voice caching
             * and decoding, over an IChatterboxTurboNetworks seam. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/SynthesisSubsystem/OnnxChatterboxTurboSynthesisEngine.cs", "src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/SynthesisSubsystem/ChatterboxTurboGenerationResult.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/SynthesisSubsystem/OnnxChatterboxTurboSynthesisEngineTests.cs");
            designFiles = ("docs/design/speech-onnx-chatterboxturbo/synthesis-subsystem/onnx-chatterboxturbo-synthesis-engine.md");
            verificationFiles = ("docs/verification/speech-onnx-chatterboxturbo/synthesis-subsystem/onnx-chatterboxturbo-synthesis-engine.md");
            requirementFiles = ("docs/reqstream/speech-onnx-chatterboxturbo/synthesis-subsystem/onnx-chatterboxturbo-synthesis-engine.yaml");
        }

        // Tokenizes prompts, runs the networks with the token selector, and returns audio via the backend
        // contract.
        dependency OnnxChatterboxTurboSynthesisEngine to ByteLevelBpeTokenizer;
        dependency OnnxChatterboxTurboSynthesisEngine to ChatterboxTurboCapabilityProfile;
        dependency OnnxChatterboxTurboSynthesisEngine to ChatterboxTurboConditioning;
        dependency OnnxChatterboxTurboSynthesisEngine to ChatterboxTurboReferenceVoice;
        dependency OnnxChatterboxTurboSynthesisEngine to SpeechOnnxChatterboxTurbo_ChatterboxTurboTokenSelector::ChatterboxTurboTokenSelector;
        dependency OnnxChatterboxTurboSynthesisEngine to SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboNetworks::OnnxChatterboxTurboNetworks;
        dependency OnnxChatterboxTurboSynthesisEngine to Speech_BackendAudio::BackendAudio;
        dependency OnnxChatterboxTurboSynthesisEngine to Speech_SynthesisBackend::SynthesisBackend;
    }
}
