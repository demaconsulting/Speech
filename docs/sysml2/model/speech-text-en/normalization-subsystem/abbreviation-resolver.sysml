package SpeechTextEn {
    part def AbbreviationResolver {
        doc /* Internal resolver of abbreviations: loads the embedded, self-authored
             * abbreviations lexicon with strict validation, looks a token up case-sensitively,
             * and picks between alternative readings of one form (for example St. as Saint or
             * Street) from the neighboring tokens alone, counting the choices that fell back to
             * the default reading. It also recognizes the name suffixes and credentials (Jr.,
             * Ph.D., III) that continue a name. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/AbbreviationResolver.cs, src/DemaConsulting.Speech.Text.En/Resources/abbreviations.tsv */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/AbbreviationResolverTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/normalization-subsystem/abbreviation-resolver.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/normalization-subsystem/abbreviation-resolver.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/normalization-subsystem/abbreviation-resolver.yaml */
    }
}
