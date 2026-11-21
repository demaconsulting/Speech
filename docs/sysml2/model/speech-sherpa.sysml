package SpeechSherpa {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;
    private import SpeechSherpa_ModelManagementSubsystem::*;
    private import SpeechSherpa_RecognitionSubsystem::*;
    private import SpeechSherpa_SynthesisSubsystem::*;

    doc
    /*
     * SpeechSherpa is the DemaConsulting.Speech.Sherpa library that supplies concrete, sherpa-onnx-backed speech
     * models and inference backends for the Speech library through its IRecognitionModel/ISynthesisModel extension
     * seam. Speech must never depend on it.
     */

    part def SpeechSherpaSystem {
        doc /* The SpeechSherpa library as a whole. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-sherpa.md");
            verificationFiles = ("docs/verification/speech-sherpa.md");
            requirementFiles = ("docs/reqstream/speech-sherpa.yaml");
        }

        part modelManagementSubsystem : ModelManagementSubsystem;
        part recognitionSubsystem : RecognitionSubsystem;
        part synthesisSubsystem : SynthesisSubsystem;
        part sherpaOnnx : SherpaOnnx;
        part sharpCompress : SharpCompress;
    }
}
