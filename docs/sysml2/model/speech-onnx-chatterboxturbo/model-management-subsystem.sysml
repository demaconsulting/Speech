package SpeechOnnxChatterboxTurbo_ModelManagementSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxChatterboxTurbo_ByteLevelBpeTokenizer::*;
    private import SpeechOnnxChatterboxTurbo_ChatterboxTurboReferenceVoice::*;
    private import SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboEnglishSynthesisModel::*;
    private import SpeechOnnxChatterboxTurbo_SpeechModelCatalogChatterboxTurboExtensions::*;

    part def ModelManagementSubsystem {
        doc /* The AddChatterboxTurboModels() catalog registration entry point, the one ONNX-Runtime-backed
             * OnnxChatterboxTurboEnglishSynthesisModel implementing Speech's ISynthesisModel contract, the embedded
             * ByteLevelBpeTokenizer, and the ChatterboxTurboReferenceVoice type that normalizes caller-supplied
             * clips. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-onnx-chatterboxturbo/model-management-subsystem.md");
            verificationFiles = ("docs/verification/speech-onnx-chatterboxturbo/model-management-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-onnx-chatterboxturbo/model-management-subsystem.yaml");
        }

        part speechModelCatalogChatterboxTurboExtensions : SpeechModelCatalogChatterboxTurboExtensions;
        part onnxChatterboxTurboEnglishSynthesisModel : OnnxChatterboxTurboEnglishSynthesisModel;
        part byteLevelBpeTokenizer : ByteLevelBpeTokenizer;
        part chatterboxTurboReferenceVoice : ChatterboxTurboReferenceVoice;
    }
}
