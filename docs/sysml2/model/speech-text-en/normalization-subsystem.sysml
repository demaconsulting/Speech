package SpeechTextEn {
    part def NormalizationSubsystem {
        doc /* The English text normalization: the alignment contract (the public NormalizationKind
             * and CliticKind enumerations and the internal builder and result that enforce
             * span invariants and reconcile sentence breaks), the TextNormalizer that decides
             * how abbreviations, symbols, apostrophes, initialisms and numeric expressions are
             * spoken (applying the host's token directives first), as spans with exact source offsets (the text itself is never rewritten), the
             * NumericExpressionRecognizer and NumberVerbalizer that read numbers, currency, units,
             * times and dates, the WebAddressRecognizer that reads URLs, e-mail addresses and
             * domains, the AlphanumericTokenRecognizer that reads identifiers such as A4 and
             * COVID-19, the lexicon-backed AbbreviationResolver, and the AcronymRecognizer.
             * Production code of this subsystem is independent of the DemaConsulting.PartOfSpeech
             * package and of the Analysis subsystem; the Analysis subsystem depends on it, never
             * the reverse. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/NamespaceDoc.cs */
        comment designRef /* Design: docs/design/speech-text-en/normalization-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/normalization-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/normalization-subsystem.yaml */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/NormalizationSubsystemTests.cs */

        part normalizationAlignment : NormalizationAlignment;
        part textNormalizer : TextNormalizer;
        part numericExpressionRecognizer : NumericExpressionRecognizer;
        part numberVerbalizer : NumberVerbalizer;
        part webAddressRecognizer : WebAddressRecognizer;
        part alphanumericTokenRecognizer : AlphanumericTokenRecognizer;
        part abbreviationResolver : AbbreviationResolver;
        part acronymRecognizer : AcronymRecognizer;
    }
}
