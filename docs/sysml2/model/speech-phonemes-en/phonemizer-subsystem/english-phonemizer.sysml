package SpeechPhonemesEn {
    part def EnglishPhonemizer {
        doc /* The public entry point of the package: phonemizes the text of one segment, with or
             * without the annotations of the English text analysis, by building the spoken text,
             * tokenizing it and pronouncing each token (applying host pronunciation overrides and
             * readings to unchanged original words), and returns a PhonemizedText of PhonemizedItem
             * values (word, punctuation or unsupported kind, ARPAbet, source, reading, out-of-vocabulary
             * flag) with the lists of the unknown words and the unsupported tokens. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/PhonemizerSubsystem/EnglishPhonemizer.cs, src/DemaConsulting.Speech.Phonemes.En/PhonemizerSubsystem/PhonemizedText.cs, src/DemaConsulting.Speech.Phonemes.En/PhonemizerSubsystem/PhonemizedItem.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/PhonemizerSubsystemTests.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/phonemizer-subsystem/english-phonemizer.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/phonemizer-subsystem/english-phonemizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/phonemizer-subsystem/english-phonemizer.yaml */
    }
}
