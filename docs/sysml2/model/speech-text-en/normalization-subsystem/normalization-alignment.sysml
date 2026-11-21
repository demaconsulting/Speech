package SpeechTextEn_NormalizationAlignment {
    private import SoftwareItemMetadata::*;

    enum def NormalizationKind {
        enum Abbreviation;
        enum Initialism;
        enum Symbol;
        enum Apostrophe;
        enum Dropped;
        enum Number;
        enum Currency;
        enum DateTime;
        enum Unit;
        enum Override;
    }

    enum def CliticKind {
        enum None;
        enum Possessive;
        enum Is;
        enum Has;
        enum Would;
        enum Had;
        enum Will;
        enum Have;
        enum Are;
        enum Am;
        enum Not;
        enum Us;
    }

    enum def TokenDirectiveKind {
        enum Skip;
        enum Replace;
        enum Spell;
        enum Pronounce;
        enum Reading;
    }

    part def NormalizationAlignment {
        doc /* The alignment contract of the normalization: the public NormalizationKind (including the host-override
             * kind) and CliticKind enumerations and the internal token, span, pronunciation mark, directive, segment,
             * builder and result types. The builder rejects any span or pronunciation mark that would break an
             * alignment invariant (overlap, wrong order, empty span, whitespace edge, surrogate cut, a spelled span
             * that is not a letter sequence), counts the overrides applied and ignored, and the result exposes the
             * spans, the pronunciation marks, the identity gaps, the spoken text and the reconciled sentence breaks;
             * it also reports whether the previous span directly touches a position and ends with a letter. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/NormalizationAlignment.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/NormalizationAlignmentTests.cs");
            designFiles = ("docs/design/speech-text-en/normalization-subsystem/normalization-alignment.md");
            verificationFiles = ("docs/verification/speech-text-en/normalization-subsystem/normalization-alignment.md");
            requirementFiles = ("docs/reqstream/speech-text-en/normalization-subsystem/normalization-alignment.yaml");
        }

        // Defines aligned replacement types whose offsets refer to text analysis tokens.
        dependency NormalizationAlignment to Speech_TextAnalysis::TextAnalysis;
    }
}
