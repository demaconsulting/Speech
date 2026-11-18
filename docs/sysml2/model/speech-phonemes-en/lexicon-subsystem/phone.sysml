package SpeechPhonemesEn {
    part def Phone {
        doc /* Internal value types of the package: the one-byte ARPAbet phone (consonants and each
             * vowel in three stress classes), the immutable pronunciation sequence with value equality
             * and the dictionary's text form, and the table of the names of the letters as
             * pronunciations (so a spelled A or I is a letter, never the article or the pronoun). */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/Phone.cs, src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/Pronunciation.cs, src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/LetterNames.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/lexicon-subsystem/phone.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/lexicon-subsystem/phone.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/lexicon-subsystem/phone.yaml */
    }
}
