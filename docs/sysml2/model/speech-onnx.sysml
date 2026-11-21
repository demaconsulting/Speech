package SpeechOnnx {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;
    private import SpeechOnnx_OnnxRuntimeSubsystem::*;

    doc
    /*
     * SpeechOnnx is the DemaConsulting.Speech.Onnx library that supplies generic ONNX Runtime infrastructure - today,
     * execution-provider probing and selection - reusable by any ONNX-Runtime-backed model package built on top of it
     * (for example the sibling DemaConsulting.Speech.Onnx.Kokoro, DemaConsulting.Speech.Onnx.NemotronStt and
     * DemaConsulting.Speech.Onnx.ChatterboxTurbo packages). It declares zero models itself and has no project
     * reference to the core DemaConsulting.Speech library.
     */

    part def SpeechOnnxSystem {
        doc /* The SpeechOnnx library as a whole. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-onnx.md");
            verificationFiles = ("docs/verification/speech-onnx.md");
            requirementFiles = ("docs/reqstream/speech-onnx.yaml");
        }

        part onnxRuntimeSubsystem : OnnxRuntimeSubsystem;
        part microsoftMlOnnxRuntime : MicrosoftMlOnnxRuntime;
    }
}
