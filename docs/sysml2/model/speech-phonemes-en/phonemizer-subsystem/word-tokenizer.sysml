package SpeechPhonemesEn {
    part def WordTokenizer {
        doc /* Internal preparation of the text for pronunciation: builds the spoken text by applying
             * the normalization and spell-out replacement annotations while remembering what is
             * original text (SpokenText), indexes the reading and pronunciation annotations by the
             * original span they cover (AnnotationIndex), and cuts the spoken text into word, letter,
             * digit, clitic, punctuation and unsupported tokens by a fixed rule order. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/PhonemizerSubsystem/WordTokenizer.cs, src/DemaConsulting.Speech.Phonemes.En/PhonemizerSubsystem/SpokenText.cs, src/DemaConsulting.Speech.Phonemes.En/PhonemizerSubsystem/AnnotationIndex.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/PhonemizerSubsystemTests.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/phonemizer-subsystem/word-tokenizer.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/phonemizer-subsystem/word-tokenizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/phonemizer-subsystem/word-tokenizer.yaml */
    }
}
