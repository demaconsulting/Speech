package SpeechOnnxPiper_ModelManagementSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxPiper_OnnxPiperLibriTtsEnSynthesisModel::*;
    private import SpeechOnnxPiper_PiperVoiceConfig::*;
    private import SpeechOnnxPiper_SpeechModelCatalogPiperExtensions::*;

    part def ModelManagementSubsystem {
        doc /* The AddPiperModels() catalog registration entry point, the one concrete
             * OnnxPiperLibriTtsEnSynthesisModel implementing Speech's ISynthesisModel contract (with the archive
             * installer it owns), and the PiperVoiceConfig reader of the voice configuration. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Onnx.Piper.Tests/ModelManagementSubsystem/ModelManagementSubsystemTests.cs");
            designFiles = ("docs/design/speech-onnx-piper/model-management-subsystem.md");
            verificationFiles = ("docs/verification/speech-onnx-piper/model-management-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-onnx-piper/model-management-subsystem.yaml");
        }

        part speechModelCatalogPiperExtensions : SpeechModelCatalogPiperExtensions;
        part onnxPiperLibriTtsEnSynthesisModel : OnnxPiperLibriTtsEnSynthesisModel;
        part piperVoiceConfig : PiperVoiceConfig;
    }
}
