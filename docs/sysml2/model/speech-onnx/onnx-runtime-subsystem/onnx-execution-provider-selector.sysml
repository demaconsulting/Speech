package SpeechOnnx {
    part def OnnxExecutionProviderSelector {
        doc /* Static helper that builds an ONNX Runtime InferenceSession for a model path,
             * trying each of an ordered list of preferred execution provider names in turn by
             * constructing a real InferenceSession with a fresh SessionOptions per candidate,
             * disposing that SessionOptions after each attempt (win or lose) without disposing
             * the returned InferenceSession on success, and falling back to the CPU provider
             * (no explicit provider) as the final, un-failing candidate. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx/OnnxRuntimeSubsystem/OnnxExecutionProviderSelector.cs */
        comment designRef /* Design: docs/design/speech-onnx/onnx-runtime-subsystem/onnx-execution-provider-selector.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx/onnx-runtime-subsystem/onnx-execution-provider-selector.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx/onnx-runtime-subsystem/onnx-execution-provider-selector.yaml */
    }
}
