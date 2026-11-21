package SpeechTextEn_EnglishTextAnalyzer {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_AnalysisDiagnostics::*;
    private import SpeechTextEn_EnglishTextAnalyzerOptions::*;
    private import SpeechTextEn_HeteronymClassifier::*;
    private import SpeechTextEn_HeteronymFeatureExtractor::*;
    private import SpeechTextEn_NormalizationAlignment::*;
    private import SpeechTextEn_PartOfSpeechAdapter::*;
    private import SpeechTextEn_PronunciationOverrides::*;
    private import SpeechTextEn_ReadingInventory::*;
    private import SpeechTextEn_TextEnAnnotations::*;
    private import SpeechTextEn_TextNormalizer::*;

    part def EnglishTextAnalyzer {
        doc /* The public ITextAnalyzer implementation: annotates the word tokens of a speech request with their
             * Universal Dependencies part of speech, annotates the spans the Normalization subsystem finds with their
             * spoken form (en.norm) or, for letter sequences, as letters to spell (en.spell), applies the host's
             * pronunciation overrides (resolved by PronunciationOverrides; en.norm, en.spell, en.pron, a fixed
             * reading or nothing for a word), attaches an en.reading annotation with its en.reading-confidence to a
             * heteronym word (a classified reading from the embedded model, or from a provider that supplies one;
             * none when the analyzer has no model source; a fixed reading from a host override with confidence
             * 1.000), and supplies authoritative sentence breaks, with every offset indexing the exact analyzed text
             * and tag placeholders neutralized to same-length spaces. Also returns the detailed, local-only
             * EnglishTextAnalysis (with its AnalyzedToken and AnalyzedNormalization records). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/EnglishTextAnalyzer.cs", "src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/EnglishTextAnalysis.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/EnglishTextAnalyzerTests.cs", "test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/EnglishTextAnalyzerReadingTests.cs");
            designFiles = ("docs/design/speech-text-en/analysis-subsystem/english-text-analyzer.md");
            verificationFiles = ("docs/verification/speech-text-en/analysis-subsystem/english-text-analyzer.md");
            requirementFiles = ("docs/reqstream/speech-text-en/analysis-subsystem/english-text-analyzer.yaml");
        }

        // Runs tagging, normalization, overrides and heteronym classification, then emits annotations and
        // diagnostics.
        dependency EnglishTextAnalyzer to AnalysisDiagnostics;
        dependency EnglishTextAnalyzer to EnglishTextAnalyzerOptions;
        dependency EnglishTextAnalyzer to HeteronymClassifier;
        dependency EnglishTextAnalyzer to HeteronymFeatureExtractor;
        dependency EnglishTextAnalyzer to NormalizationAlignment;
        dependency EnglishTextAnalyzer to PartOfSpeechAdapter;
        dependency EnglishTextAnalyzer to PronunciationOverrides;
        dependency EnglishTextAnalyzer to ReadingInventory;
        dependency EnglishTextAnalyzer to TextEnAnnotations;
        dependency EnglishTextAnalyzer to TextNormalizer;
        dependency EnglishTextAnalyzer to Speech_TextAnalysis::TextAnalysis;
    }
}
