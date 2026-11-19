package SpeechOnnxPiper {
    part def ModelManagementSubsystem {
        doc /* The AddPiperModels() catalog registration entry point, the one concrete
             * OnnxPiperLibriTtsEnSynthesisModel implementing Speech's ISynthesisModel contract (with the
             * archive installer it owns), and the PiperVoiceConfig reader of the voice configuration. */

        comment designRef /* Design: docs/design/speech-onnx-piper/model-management-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-piper/model-management-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-piper/model-management-subsystem.yaml */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.Piper.Tests/ModelManagementSubsystem/ModelManagementSubsystemTests.cs */

        part speechModelCatalogPiperExtensions : SpeechModelCatalogPiperExtensions;
        part onnxPiperLibriTtsEnSynthesisModel : OnnxPiperLibriTtsEnSynthesisModel;
        part piperVoiceConfig : PiperVoiceConfig;
    }
}
