package SpeechOnnxKokoro {
    part def KokoroLexiconPhonemizer {
        doc /* Converts English text into Kokoro v1.0's own IPA phoneme string by looking each
             * word up in an embedded, precomputed word-to-phoneme lexicon (produced by running
             * the real misaki.en.G2P tool offline over the CMUdict word list), rather than
             * reimplementing grapheme-to-phoneme linguistics by hand. Honestly documents two
             *              Stage-1 limitations: words with no lexicon entry and no near spelling match are omitted
                          * and reported rather than guessed (corrected or near-match words are spoken and not
                          * reported), and homographs resolve to one context-free pronunciation. Text is first
                          * normalized by the internal KokoroTextNormalizer helper. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.Kokoro/ModelManagementSubsystem/KokoroLexiconPhonemizer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.Kokoro.Tests/ModelManagementSubsystem/KokoroLexiconPhonemizerTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-kokoro/model-management-subsystem/kokoro-lexicon-phonemizer.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-kokoro/model-management-subsystem/kokoro-lexicon-phonemizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-kokoro/model-management-subsystem/kokoro-lexicon-phonemizer.yaml */
    }
}
