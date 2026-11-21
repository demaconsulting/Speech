package SpeechTextEn_AbbreviationResolver {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_NormalizationAlignment::*;
    private import SpeechTextEn_NumericExpressionRecognizer::*;

    enum def AbbreviationClass {
        enum Prefix;
        enum Suffix;
        enum Free;
        enum Word;
    }

    enum def GuardKinds {
        enum None;
        enum NextCap;
        enum NextDigit;
        enum PrevNotNumber;
        enum NextDate;
        enum PrevNumber;
    }

    part def AbbreviationResolver {
        doc /* Internal resolver of abbreviations: loads the embedded, self-authored abbreviations lexicon with strict
             * validation, looks a token up case-sensitively, and picks between alternative readings of one form (for
             * example St. as Saint or Street) from the neighboring tokens alone, counting the choices that fell back
             * to the default reading. It also holds the area-unit words sq. and ft. (read where the numeric rule does
             * not take them), the month and weekday abbreviations, read as a prefix before a date and as a suffix
             * elsewhere, and recognizes the name suffixes and credentials (Jr., Ph.D., III) that continue a name. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/AbbreviationResolver.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/AbbreviationResolverTests.cs");
            designFiles = ("docs/design/speech-text-en/normalization-subsystem/abbreviation-resolver.md");
            verificationFiles = ("docs/verification/speech-text-en/normalization-subsystem/abbreviation-resolver.md");
            requirementFiles = ("docs/reqstream/speech-text-en/normalization-subsystem/abbreviation-resolver.yaml");
        }

        // Expands lexicon abbreviations as aligned replacements, deferring numeric context to the numeric
        // recognizer.
        dependency AbbreviationResolver to NormalizationAlignment;
        dependency AbbreviationResolver to NumericExpressionRecognizer;
    }
}
