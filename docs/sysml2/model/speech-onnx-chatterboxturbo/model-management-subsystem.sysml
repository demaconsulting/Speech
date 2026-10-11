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

    part def SpeechModelCatalogChatterboxTurboExtensions {
        doc /* Public AddChatterboxTurboModels() extension method that registers the one
             * ONNX-Runtime-backed Chatterbox Turbo model with a SpeechModelCatalog and returns
             * the same catalog for chaining. Documented as a subsystem unit (no separate
             * unit-level companion files). */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/SpeechModelCatalogChatterboxTurboExtensions.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/ModelManagementSubsystem/OnnxChatterboxTurboEnglishSynthesisModelTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-chatterboxturbo/model-management-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-onnx-chatterboxturbo/model-management-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-chatterboxturbo/model-management-subsystem.yaml (consolidated at subsystem level) */
    }
}
