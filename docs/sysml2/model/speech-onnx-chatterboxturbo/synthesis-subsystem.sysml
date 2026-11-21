package SpeechOnnxChatterboxTurbo_SynthesisSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxChatterboxTurbo_ChatterboxTurboCapabilityProfile::*;
    private import SpeechOnnxChatterboxTurbo_ChatterboxTurboConditioning::*;
    private import SpeechOnnxChatterboxTurbo_ChatterboxTurboTokenSelector::*;
    private import SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboNetworks::*;
    private import SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboSynthesisEngine::*;

    part def SynthesisSubsystem {
        doc /* The real ONNX-Runtime-backed implementation of Speech's public ISynthesisBackend seam over the four
             * Chatterbox Turbo graphs: the generation engine, the ONNX networks, the repetition-penalized token
             * selector, the reference-clip conditioning bundle, and the audio-tag capability profile. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-onnx-chatterboxturbo/synthesis-subsystem.md");
            verificationFiles = ("docs/verification/speech-onnx-chatterboxturbo/synthesis-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-onnx-chatterboxturbo/synthesis-subsystem.yaml");
        }

        part onnxChatterboxTurboSynthesisEngine : OnnxChatterboxTurboSynthesisEngine;
        part onnxChatterboxTurboNetworks : OnnxChatterboxTurboNetworks;
        part chatterboxTurboTokenSelector : ChatterboxTurboTokenSelector;
        part chatterboxTurboCapabilityProfile : ChatterboxTurboCapabilityProfile;
        part chatterboxTurboConditioning : ChatterboxTurboConditioning;
    }
}
