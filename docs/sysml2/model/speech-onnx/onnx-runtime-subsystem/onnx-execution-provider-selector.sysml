package SpeechOnnx {
    part def OnnxExecutionProviderSelector {
        doc /* Static helper that builds an ONNX Runtime InferenceSession for a model path,
             * trying each of an ordered list of preferred execution provider names in turn by
             * constructing a real InferenceSession with a fresh SessionOptions per candidate,
             * disposing that SessionOptions after each attempt (win or lose) without disposing
             * the returned InferenceSession on success, and falling back to the CPU provider
             * (no explicit provider) as the final, un-failing candidate. Accepts an optional
             * validateSession probe run against each accelerated candidate session before it is
             * accepted (never run against the final CPU fallback), so a candidate that
             * constructs successfully but fails on the real graph at Run() time is rejected and
             * the next candidate is tried; disposes the candidate session on ANY exception from
             * construction or the probe (try/finally with a success flag), rethrowing any
             * exception type other than OnnxRuntimeException/DllNotFoundException after
             * disposal rather than swallowing it. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx/OnnxRuntimeSubsystem/OnnxExecutionProviderSelector.cs */
        comment designRef /* Design: docs/design/speech-onnx/onnx-runtime-subsystem/onnx-execution-provider-selector.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx/onnx-runtime-subsystem/onnx-execution-provider-selector.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx/onnx-runtime-subsystem/onnx-execution-provider-selector.yaml */
    }
}
