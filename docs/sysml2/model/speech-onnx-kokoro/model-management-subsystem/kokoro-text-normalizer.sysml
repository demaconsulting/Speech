package SpeechOnnxKokoro {
    part def KokoroTextNormalizer {
        doc /* Internal static helper that rewrites text into a form the lexicon phonemizer can
             * speak: numbers, currency, times, percentages, years and ordinals are written as
             * words, markdown markers are removed, URLs and e-mail addresses are read aloud,
             * common abbreviations are expanded, and diacritics are stripped. All-caps words
             * are marked for letter-by-letter spelling. Documented and verified as a supporting
             * helper of KokoroLexiconPhonemizer (no separate unit-level companion files). */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.Kokoro/ModelManagementSubsystem/KokoroTextNormalizer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.Kokoro.Tests/ModelManagementSubsystem/KokoroTextNormalizerTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-kokoro/model-management-subsystem/kokoro-lexicon-phonemizer.md (documented as a supporting helper of KokoroLexiconPhonemizer) */
        comment verificationRef /* Verification: docs/verification/speech-onnx-kokoro/model-management-subsystem/kokoro-lexicon-phonemizer.md (documented as a supporting helper of KokoroLexiconPhonemizer) */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-kokoro/model-management-subsystem/kokoro-lexicon-phonemizer.yaml (consolidated with KokoroLexiconPhonemizer) */
    }
}
