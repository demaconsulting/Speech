package SpeechPhonemesEn {
    part def WordPronouncer {
        doc /* Internal pronunciation of one word by the priority list: the reading the text analysis
             * chose, the reviewed correction of a wrong dictionary entry, the dictionary's first variant,
             * the morphology derivation from a known stem, a productive prefix plus a known word, and the
             * letter-to-sound model, reporting the source of the answer; folds diacritics, pronounces
             * hyphenated compounds part by part with the stress of one word (the closed dictionary
             * spelling's pattern when it has the same sounds; otherwise one primary stress on the first
             * element, or on the root after a productive prefix such as re-, non- or ex-) and records
             * where each element starts, and speaks letters and digits. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/PhonemizerSubsystem/WordPronouncer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/PhonemizerSubsystemTests.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/phonemizer-subsystem/word-pronouncer.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/phonemizer-subsystem/word-pronouncer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/phonemizer-subsystem/word-pronouncer.yaml */
    }
}
