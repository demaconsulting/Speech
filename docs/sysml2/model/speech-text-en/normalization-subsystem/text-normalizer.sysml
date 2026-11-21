package SpeechTextEn_TextNormalizer {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_AbbreviationResolver::*;
    private import SpeechTextEn_AcronymRecognizer::*;
    private import SpeechTextEn_AlphanumericTokenRecognizer::*;
    private import SpeechTextEn_NormalizationAlignment::*;
    private import SpeechTextEn_NumericExpressionRecognizer::*;
    private import SpeechTextEn_WebAddressRecognizer::*;

    part def TextNormalizer {
        doc /* Internal, stateless orchestrator that makes one linear pass over the tokens of a text and produces
             * aligned replacements for host override directives (applied first, before every built-in rule, which
             * also produce pronunciation marks; the abbreviation rules' sentence-break edits are still made for the
             * directive's token), numeric expressions (through the NumericExpressionRecognizer), abbreviations,
             * initialisms (marked as spelled letter sequences, with ampersand initialisms decomposed into letter and
             * "and" spans), web addresses (through the WebAddressRecognizer), identifiers of letters and digits
             * (through the AlphanumericTokenRecognizer), symbols and marks, typographic apostrophes and invisible
             * format characters, optionally classifying clitics. It decides which sentence breaks a consumed period
             * forces or suppresses (including keeping a name suffix together with its credentials and a date
             * together), applies the abbreviation period rules to units such as ft., counts ambiguous readings,
             * numeric fallbacks and overrides applied or ignored (a directive for a token an earlier match consumed),
             * separates adjacent word-producing replacements with exactly one space, honors cancellation, and leaves
             * every unhandled token unchanged. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/TextNormalizer.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/TextNormalizerTests.cs");
            designFiles = ("docs/design/speech-text-en/normalization-subsystem/text-normalizer.md");
            verificationFiles = ("docs/verification/speech-text-en/normalization-subsystem/text-normalizer.md");
            requirementFiles = ("docs/reqstream/speech-text-en/normalization-subsystem/text-normalizer.yaml");
        }

        // Runs the abbreviation, acronym, alphanumeric, numeric and web recognizers in one pass to make
        // aligned replacements.
        dependency TextNormalizer to AbbreviationResolver;
        dependency TextNormalizer to AcronymRecognizer;
        dependency TextNormalizer to AlphanumericTokenRecognizer;
        dependency TextNormalizer to NormalizationAlignment;
        dependency TextNormalizer to NumericExpressionRecognizer;
        dependency TextNormalizer to WebAddressRecognizer;
    }
}
