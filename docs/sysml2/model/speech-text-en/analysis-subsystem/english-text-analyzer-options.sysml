package SpeechTextEn_EnglishTextAnalyzerOptions {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_PronunciationOverrides::*;
    private import ScalarValues::*;

    enum def TextEnAnnotationSelection {
        enum None;
        enum PartOfSpeech;
        enum Normalization;
        enum Reading;
    }

    part def EnglishTextAnalyzerOptions {
        doc /* Immutable, validated options of an EnglishTextAnalyzer: the annotation families to produce (the
             * TextEnAnnotationSelection flags enumeration: part of speech, normalization and reading by default), an
             * optional counts-only diagnostics sink, an optional dictionary of pronunciation overrides (which
             * requires normalization) and the confidence below which a classified reading counts as low confidence.
             * The Reading selection flag is part of the default selection; it is effective for every heteronym word
             * under the embedded model, and an analyzer given no model source honors only words that carry a reading
             * override. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/EnglishTextAnalyzerOptions.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/EnglishTextAnalyzerOptionsTests.cs");
            designFiles = ("docs/design/speech-text-en/analysis-subsystem/english-text-analyzer-options.md");
            verificationFiles = ("docs/verification/speech-text-en/analysis-subsystem/english-text-analyzer-options.md");
            requirementFiles = ("docs/reqstream/speech-text-en/analysis-subsystem/english-text-analyzer-options.yaml");
        }

        attribute annotations : TextEnAnnotationSelection;
        attribute lowConfidenceThreshold : Real;

        // Holds the host's pronunciation overrides and diagnostics sink as validated configuration for the
        // analyzer.
        dependency EnglishTextAnalyzerOptions to PronunciationOverrides;
        dependency EnglishTextAnalyzerOptions to Speech_ISpeechDiagnostics::ISpeechDiagnostics;
    }
}
