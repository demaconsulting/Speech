package SpeechOnnxChatterboxTurbo {
    part def ModelManagementSubsystem {
        doc /* The AddChatterboxTurboModels() catalog registration entry point, the one
             * ONNX-Runtime-backed OnnxChatterboxTurboEnglishSynthesisModel implementing Speech's
             * ISynthesisModel contract, the embedded ByteLevelBpeTokenizer, and the
             * ChatterboxTurboReferenceVoice type that normalizes caller-supplied clips. */

        comment designRef /* Design: docs/design/speech-onnx-chatterboxturbo/model-management-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-chatterboxturbo/model-management-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-chatterboxturbo/model-management-subsystem.yaml */

        part speechModelCatalogChatterboxTurboExtensions : SpeechModelCatalogChatterboxTurboExtensions;
        part onnxChatterboxTurboEnglishSynthesisModel : OnnxChatterboxTurboEnglishSynthesisModel;
        part byteLevelBpeTokenizer : ByteLevelBpeTokenizer;
        part chatterboxTurboReferenceVoice : ChatterboxTurboReferenceVoice;
    }
}
