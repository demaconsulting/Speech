package SpeechTextEn {
    part def AcronymRecognizer {
        doc /* Internal recognizer of acronyms and initialisms: loads the embedded, self-authored
             * acronyms lexicon with strict validation and decides, case-sensitively, whether a
             * token is spoken letter by letter (listed forms, dotted forms, ampersand forms and
             * short all-capital tokens without a vowel), guarding against all-capital shouting. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/AcronymRecognizer.cs, src/DemaConsulting.Speech.Text.En/Resources/acronyms.tsv */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/AcronymRecognizerTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/normalization-subsystem/acronym-recognizer.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/normalization-subsystem/acronym-recognizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/normalization-subsystem/acronym-recognizer.yaml */
    }
}
