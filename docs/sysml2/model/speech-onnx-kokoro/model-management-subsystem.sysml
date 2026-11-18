package SpeechOnnxKokoro {
    part def ModelManagementSubsystem {
        doc /* The AddKokoroModels() catalog registration entry point, the one concrete
             * ONNX-Runtime-backed OnnxKokoroEnglishSynthesisModel implementing Speech's
             * ISynthesisModel contract, and the supporting KokoroPhonemeVocabulary unit that
             * converts Kokoro phoneme symbols to token ids. */

        comment designRef /* Design: docs/design/speech-onnx-kokoro/model-management-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-kokoro/model-management-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-kokoro/model-management-subsystem.yaml */

        part speechModelCatalogKokoroExtensions : SpeechModelCatalogKokoroExtensions;
        part onnxKokoroEnglishSynthesisModel : OnnxKokoroEnglishSynthesisModel;
        part kokoroPhonemeVocabulary : KokoroPhonemeVocabulary;
    }
}
