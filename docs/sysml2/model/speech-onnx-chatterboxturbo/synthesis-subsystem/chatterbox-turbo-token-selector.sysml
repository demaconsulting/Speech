package SpeechOnnxChatterboxTurbo_ChatterboxTurboTokenSelector {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboSynthesisEngine::*;

    part def ChatterboxTurboTokenSelector {
        doc /* Repetition-penalized greedy speech-token selection. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/SynthesisSubsystem/ChatterboxTurboTokenSelector.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/SynthesisSubsystem/ChatterboxTurboTokenSelectorTests.cs");
            designFiles = ("docs/design/speech-onnx-chatterboxturbo/synthesis-subsystem/chatterbox-turbo-token-selector.md");
            verificationFiles = ("docs/verification/speech-onnx-chatterboxturbo/synthesis-subsystem/chatterbox-turbo-token-selector.md");
            requirementFiles = ("docs/reqstream/speech-onnx-chatterboxturbo/synthesis-subsystem/chatterbox-turbo-token-selector.yaml");
        }

        // Picks the next speech token for the synthesis engine's autoregressive generation loop.
        dependency ChatterboxTurboTokenSelector to OnnxChatterboxTurboSynthesisEngine;
    }
}
