package SpeechOnnx {
    part def OnnxRuntimeSubsystem {
        doc /* The OnnxExecutionProviderSelector static helper that builds an ONNX Runtime
             * InferenceSession, trying each preferred accelerated execution provider in order
             * before falling back to the always-available CPU provider. */

        comment designRef /* Design: docs/design/speech-onnx/onnx-runtime-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx/onnx-runtime-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx/onnx-runtime-subsystem.yaml */

        part onnxExecutionProviderSelector : OnnxExecutionProviderSelector;
    }
}
