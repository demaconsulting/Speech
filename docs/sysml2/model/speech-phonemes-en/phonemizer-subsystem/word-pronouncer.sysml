package SpeechPhonemesEn {
    part def WordPronouncer {
        doc /* Internal pronunciation of one word by the priority list: the reading the text analysis
             * chose, the dictionary's first variant, the morphology derivation from a known stem, a
             * productive prefix plus a known word, and the letter-to-sound model, reporting the source
             * of the answer; folds diacritics, pronounces hyphenated compounds part by part and speaks
             * letters and digits. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/PhonemizerSubsystem/WordPronouncer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/PhonemizerSubsystemTests.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/phonemizer-subsystem/word-pronouncer.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/phonemizer-subsystem/word-pronouncer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/phonemizer-subsystem/word-pronouncer.yaml */
    }
}
