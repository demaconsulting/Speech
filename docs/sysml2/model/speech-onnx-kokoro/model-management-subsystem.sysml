package SpeechOnnxKokoro {
    part def ModelManagementSubsystem {
        doc /* The AddKokoroModels() catalog registration entry point, the one concrete
             * ONNX-Runtime-backed OnnxKokoroEnglishSynthesisModel implementing Speech's
             * ISynthesisModel contract, and the two supporting units
             * (KokoroLexiconPhonemizer, KokoroPhonemeVocabulary) that give it its own complete
             * text-to-phoneme-to-token pipeline. */

        comment designRef /* Design: docs/design/speech-onnx-kokoro/model-management-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-kokoro/model-management-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-kokoro/model-management-subsystem.yaml */

        part speechModelCatalogKokoroExtensions : SpeechModelCatalogKokoroExtensions;
        part onnxKokoroEnglishSynthesisModel : OnnxKokoroEnglishSynthesisModel;
        part kokoroLexiconPhonemizer : KokoroLexiconPhonemizer;
        part kokoroPhonemeVocabulary : KokoroPhonemeVocabulary;
    }

    part def SpeechModelCatalogKokoroExtensions {
        doc /* Public AddKokoroModels() extension method that registers the one
             * ONNX-Runtime-backed Kokoro model with a SpeechModelCatalog and returns the same
             * catalog for chaining. Documented as a subsystem unit (no separate unit-level
             * companion files), mirroring SpeechSherpa's SpeechModelCatalogSherpaExtensions
             * convention. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.Kokoro/SpeechModelCatalogKokoroExtensions.cs */
        comment designRef /* Design: docs/design/speech-onnx-kokoro/model-management-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-onnx-kokoro/model-management-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-kokoro/model-management-subsystem.yaml (consolidated at subsystem level) */
    }
}
