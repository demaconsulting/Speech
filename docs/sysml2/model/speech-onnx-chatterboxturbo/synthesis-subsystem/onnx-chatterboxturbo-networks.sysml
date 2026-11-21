package SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboNetworks {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxChatterboxTurbo_ChatterboxTurboConditioning::*;
    private import SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboSynthesisEngine::*;

    part def OnnxChatterboxTurboNetworks {
        doc /* The raw ONNX Runtime sessions for the speech encoder, token embedder, language model and conditional
             * decoder, with thread configuration and deterministic disposal. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/SynthesisSubsystem/OnnxChatterboxTurboNetworks.cs", "src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/SynthesisSubsystem/IChatterboxTurboNetworks.cs", "src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/SynthesisSubsystem/IChatterboxTurboLanguageModelState.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/SynthesisSubsystem/OnnxChatterboxTurboNetworksTests.cs");
            designFiles = ("docs/design/speech-onnx-chatterboxturbo/synthesis-subsystem/onnx-chatterboxturbo-networks.md");
            verificationFiles = ("docs/verification/speech-onnx-chatterboxturbo/synthesis-subsystem/onnx-chatterboxturbo-networks.md");
            requirementFiles = ("docs/reqstream/speech-onnx-chatterboxturbo/synthesis-subsystem/onnx-chatterboxturbo-networks.yaml");
        }

        // Hosts ONNX Runtime sessions that the engine calls to compute conditioning and generate tokens.
        dependency OnnxChatterboxTurboNetworks to ChatterboxTurboConditioning;
        dependency OnnxChatterboxTurboNetworks to OnnxChatterboxTurboSynthesisEngine;
        dependency OnnxChatterboxTurboNetworks to OtsDependencies::MicrosoftMlOnnxRuntime;
    }
}
