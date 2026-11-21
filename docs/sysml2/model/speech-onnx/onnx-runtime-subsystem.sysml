package SpeechOnnx_OnnxRuntimeSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnx_OnnxExecutionProviderSelector::*;

    part def OnnxRuntimeSubsystem {
        doc /* The OnnxExecutionProviderSelector static helper that builds an ONNX Runtime InferenceSession, trying
             * each preferred accelerated execution provider in order before falling back to the always-available CPU
             * provider. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-onnx/onnx-runtime-subsystem.md");
            verificationFiles = ("docs/verification/speech-onnx/onnx-runtime-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-onnx/onnx-runtime-subsystem.yaml");
        }

        part onnxExecutionProviderSelector : OnnxExecutionProviderSelector;
    }
}
