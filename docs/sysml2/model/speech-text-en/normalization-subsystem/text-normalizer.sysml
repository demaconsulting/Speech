package SpeechTextEn {
    part def TextNormalizer {
        doc /* Internal, stateless orchestrator that makes one linear pass over the tokens of a
             * text and produces aligned replacements for host override directives (applied first,
             * before every built-in rule, which also produce pronunciation marks; the abbreviation
             * rules' sentence-break edits are still made for the directive's token), numeric
             * expressions (through the NumericExpressionRecognizer), abbreviations, initialisms
             * (marked as spelled letter sequences, with ampersand initialisms decomposed into
             * letter and "and" spans), symbols and
             * marks, typographic apostrophes and invisible format characters, optionally
             * classifying clitics. It decides which sentence breaks a consumed period forces or
             * suppresses (including keeping a name suffix together with its credentials and a
             * date together), applies the abbreviation period rules to units such as ft.,
             * counts ambiguous readings, numeric fallbacks and overrides applied or ignored (a
             * directive for a token an earlier match consumed), separates
             * adjacent word-producing replacements with exactly one space, honors cancellation, and
             * leaves every unhandled token unchanged. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/TextNormalizer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/TextNormalizerTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/normalization-subsystem/text-normalizer.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/normalization-subsystem/text-normalizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/normalization-subsystem/text-normalizer.yaml */
    }
}
