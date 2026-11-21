package SpeechOnnxChatterboxTurbo {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;
    private import SpeechOnnxChatterboxTurbo_ModelManagementSubsystem::*;
    private import SpeechOnnxChatterboxTurbo_SynthesisSubsystem::*;

    doc
    /*
     * SpeechOnnxChatterboxTurbo is the DemaConsulting.Speech.Onnx.ChatterboxTurbo library that supplies a concrete,
     * ONNX-Runtime-backed Chatterbox Turbo (English, reference-clip voice cloning) text-to-speech model and inference
     * engine for the Speech library through its ISynthesisModel extension seam. Speech must never depend on it. The
     * system owns its own byte-level BPE tokenizer and its audio-tag rendering policy.
     */

    part def SpeechOnnxChatterboxTurboSystem {
        doc /* The SpeechOnnxChatterboxTurbo library as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/SpeechOnnxChatterboxTurboTests.cs");
            designFiles = ("docs/design/speech-onnx-chatterboxturbo.md");
            verificationFiles = ("docs/verification/speech-onnx-chatterboxturbo.md");
            requirementFiles = ("docs/reqstream/speech-onnx-chatterboxturbo.yaml");
        }

        part modelManagementSubsystem : ModelManagementSubsystem;
        part synthesisSubsystem : SynthesisSubsystem;
        part microsoftMlOnnxRuntime : MicrosoftMlOnnxRuntime;
    }
}
