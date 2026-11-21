package SpeechOnnx_OnnxExecutionProviderSelector {
    private import SoftwareItemMetadata::*;

    part def OnnxExecutionProviderSelector {
        doc /* Static helper that builds an ONNX Runtime InferenceSession for a model path, trying each of an ordered
             * list of preferred execution provider names in turn by constructing a real InferenceSession with a fresh
             * SessionOptions per candidate, disposing that SessionOptions after each attempt (win or lose) without
             * disposing the returned InferenceSession on success, and falling back to the CPU provider (no explicit
             * provider) as the final, un-failing candidate. Accepts an optional validateSession probe run against
             * each accelerated candidate session before it is accepted (never run against the final CPU fallback), so
             * a candidate that constructs successfully but fails on the real graph at Run() time is rejected and the
             * next candidate is tried; disposes the candidate session on ANY exception from construction or the probe
             * (try/finally with a success flag), rethrowing any exception type other than
             * OnnxRuntimeException/DllNotFoundException after disposal rather than swallowing it. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx/OnnxRuntimeSubsystem/OnnxExecutionProviderSelector.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.Tests/OnnxRuntimeSubsystem/OnnxExecutionProviderSelectorTests.cs");
            designFiles = ("docs/design/speech-onnx/onnx-runtime-subsystem/onnx-execution-provider-selector.md");
            verificationFiles = ("docs/verification/speech-onnx/onnx-runtime-subsystem/onnx-execution-provider-selector.md");
            requirementFiles = ("docs/reqstream/speech-onnx/onnx-runtime-subsystem/onnx-execution-provider-selector.yaml");
        }

        // Uses ONNX Runtime to try each preferred execution provider in turn, falling back to CPU.
        dependency OnnxExecutionProviderSelector to OtsDependencies::MicrosoftMlOnnxRuntime;
    }
}
