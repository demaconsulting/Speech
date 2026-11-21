package SpeechTextEn_NumericExpressionRecognizer {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_NormalizationAlignment::*;
    private import SpeechTextEn_NumberVerbalizer::*;

    enum def Shape {
        enum Plain;
        enum Dotted;
        enum Time;
        enum Slash;
        enum Hyphen;
        enum Bad;
        enum Other;
    }

    part def NumericExpressionRecognizer {
        doc /* Internal, stateless, regular-expression-free recognizer of the numeric expressions of en-US English
             * text: numbers, ordinals, decimals, fractions, years and decades, currency, percent, units, times, dates
             * and digit groups, plus the number of a hyphenated compound (12-year-old) and a number with a capital
             * letter glued to it (10K). It classifies whole tokens by shape with bounded look-behind and look-ahead,
             * decomposes a construct into one span per token (or a currency symbol or apostrophe with its adjacent
             * token) so that no span contains whitespace, reports readings chosen by default as ambiguous and numbers
             * left as written because of a documented limit as numeric fallbacks, and builds its words with the
             * NumberVerbalizer. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/NumericExpressionRecognizer.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/NumericExpressionRecognizerTests.cs");
            designFiles = ("docs/design/speech-text-en/normalization-subsystem/numeric-expression-recognizer.md");
            verificationFiles = ("docs/verification/speech-text-en/normalization-subsystem/numeric-expression-recognizer.md");
            requirementFiles = ("docs/reqstream/speech-text-en/normalization-subsystem/numeric-expression-recognizer.yaml");
        }

        // Recognizes numeric expressions and verbalizes them through the number verbalizer as aligned
        // replacements.
        dependency NumericExpressionRecognizer to NormalizationAlignment;
        dependency NumericExpressionRecognizer to NumberVerbalizer;
    }
}
