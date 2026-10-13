package SpeechOnnxChatterboxTurbo {
    part def SynthesisSubsystem {
        doc /* The real ONNX-Runtime-backed implementation of Speech's public ISynthesisBackend
             * seam over the four Chatterbox Turbo graphs: the generation engine, the ONNX
             * networks, the repetition-penalized token selector, the reference-clip
             * conditioning bundle, and the audio-tag capability profile. */

        comment designRef /* Design: docs/design/speech-onnx-chatterboxturbo/synthesis-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-chatterboxturbo/synthesis-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-chatterboxturbo/synthesis-subsystem.yaml */

        part onnxChatterboxTurboSynthesisEngine : OnnxChatterboxTurboSynthesisEngine;
        part onnxChatterboxTurboNetworks : OnnxChatterboxTurboNetworks;
        part chatterboxTurboTokenSelector : ChatterboxTurboTokenSelector;
        part chatterboxTurboCapabilityProfile : ChatterboxTurboCapabilityProfile;
        part chatterboxTurboConditioning : ChatterboxTurboConditioning;
    }
}
