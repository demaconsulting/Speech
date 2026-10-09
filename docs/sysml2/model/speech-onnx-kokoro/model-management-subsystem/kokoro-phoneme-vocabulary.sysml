package SpeechOnnxKokoro {
    part def KokoroPhonemeVocabulary {
        doc /* Loads Kokoro v1.0's own character-level IPA phoneme vocabulary from an embedded
             * copy of its tokenizer.json, and converts a phoneme string into the integer token
             * id sequence the ONNX model expects, dropping any unmapped character and
             * truncating to MaxPhonemeTokens (510) entries. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.Kokoro/ModelManagementSubsystem/KokoroPhonemeVocabulary.cs */
        comment designRef /* Design: docs/design/speech-onnx-kokoro/model-management-subsystem/kokoro-phoneme-vocabulary.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-kokoro/model-management-subsystem/kokoro-phoneme-vocabulary.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-kokoro/model-management-subsystem/kokoro-phoneme-vocabulary.yaml */
    }
}
