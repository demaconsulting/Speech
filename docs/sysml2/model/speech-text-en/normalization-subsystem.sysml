package SpeechTextEn_NormalizationSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_AbbreviationResolver::*;
    private import SpeechTextEn_AcronymRecognizer::*;
    private import SpeechTextEn_AlphanumericTokenRecognizer::*;
    private import SpeechTextEn_NormalizationAlignment::*;
    private import SpeechTextEn_NumberVerbalizer::*;
    private import SpeechTextEn_NumericExpressionRecognizer::*;
    private import SpeechTextEn_TextNormalizer::*;
    private import SpeechTextEn_WebAddressRecognizer::*;

    part def NormalizationSubsystem {
        doc /* The English text normalization: the alignment contract (the public NormalizationKind and CliticKind
             * enumerations and the internal builder and result that enforce span invariants and reconcile sentence
             * breaks), the TextNormalizer that decides how abbreviations, symbols, apostrophes, initialisms and
             * numeric expressions are spoken (applying the host's token directives first), as spans with exact source
             * offsets (the text itself is never rewritten), the NumericExpressionRecognizer and NumberVerbalizer that
             * read numbers, currency, units, times and dates, the WebAddressRecognizer that reads URLs, e-mail
             * addresses and domains, the AlphanumericTokenRecognizer that reads identifiers such as A4 and COVID-19,
             * the lexicon-backed AbbreviationResolver, and the AcronymRecognizer. Production code of this subsystem
             * is independent of the DemaConsulting.PartOfSpeech package and of the Analysis subsystem; the Analysis
             * subsystem depends on it, never the reverse. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/NormalizationSubsystemTests.cs");
            designFiles = ("docs/design/speech-text-en/normalization-subsystem.md");
            verificationFiles = ("docs/verification/speech-text-en/normalization-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-text-en/normalization-subsystem.yaml");
        }

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
