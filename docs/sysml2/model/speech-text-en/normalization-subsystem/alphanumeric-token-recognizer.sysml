package SpeechTextEn_AlphanumericTokenRecognizer {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_NormalizationAlignment::*;
    private import SpeechTextEn_NumberVerbalizer::*;
    private import SpeechTextEn_NumericExpressionRecognizer::*;

    part def AlphanumericTokenRecognizer {
        doc /* Internal, stateless recognizer of tokens that mix letters and digits in the way of identifiers and
             * names: alternating runs of capital letters (spelled) and digits (read as a cardinal up to three digits
             * without a leading zero, digit by digit otherwise) such as A4, H2O and MP3, and a name joined to one to
             * three digits by a hyphen (COVID-19, Catch-22), of which only the number is read. It leaves a word of
             * four or more capital letters before digits and every other shape alone, and the work per token is
             * bounded. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/AlphanumericTokenRecognizer.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/AlphanumericTokenRecognizerTests.cs");
            designFiles = ("docs/design/speech-text-en/normalization-subsystem/alphanumeric-token-recognizer.md");
            verificationFiles = ("docs/verification/speech-text-en/normalization-subsystem/alphanumeric-token-recognizer.md");
            requirementFiles = ("docs/reqstream/speech-text-en/normalization-subsystem/alphanumeric-token-recognizer.yaml");
        }

        // Spells letter runs and verbalizes digit runs with the number verbalizer, emitting aligned
        // replacements.
        dependency AlphanumericTokenRecognizer to NormalizationAlignment;
        dependency AlphanumericTokenRecognizer to NumberVerbalizer;
        dependency AlphanumericTokenRecognizer to NumericExpressionRecognizer;
    }
}
