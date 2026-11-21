package SpeechTextEn_AnalysisSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_AnalysisDiagnostics::*;
    private import SpeechTextEn_EnglishTextAnalyzer::*;
    private import SpeechTextEn_EnglishTextAnalyzerOptions::*;
    private import SpeechTextEn_PartOfSpeechAdapter::*;
    private import SpeechTextEn_PronunciationOverrides::*;
    private import SpeechTextEn_TextEnAnnotations::*;

    part def AnalysisSubsystem {
        doc /* The English text analyzer (EnglishTextAnalyzer and its detailed-result types), its options, the
             * annotation vocabulary and typed readers (TextEnAnnotations), the immutable dictionary of host-supplied
             * pronunciation overrides (PronunciationOverrides), the internal adapter that isolates the
             * DemaConsulting.PartOfSpeech package (PartOfSpeechAdapter), and the internal counts-only diagnostics
             * reporter (AnalysisDiagnostics). The analyzer resolves the override dictionary to token directives and
             * drives the Normalization subsystem (NormalizationSubsystem) to produce en.norm, en.spell and en.pron
             * annotations and reconciled sentence breaks; this dependency is one way, from Analysis to Normalization.
             * It also asks the Heteronym subsystem (HeteronymSubsystem) for the reading of heteronym words and emits
             * the en.reading and en.reading-confidence annotations (from a reading override, or from the classifier
             * over the embedded model, which the default analyzer has); this dependency is one way, from Analysis to
             * Heteronym. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/AnalysisSubsystemTests.cs");
            designFiles = ("docs/design/speech-text-en/analysis-subsystem.md");
            verificationFiles = ("docs/verification/speech-text-en/analysis-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-text-en/analysis-subsystem.yaml");
        }

        part englishTextAnalyzer : EnglishTextAnalyzer;
        part englishTextAnalyzerOptions : EnglishTextAnalyzerOptions;
        part textEnAnnotations : TextEnAnnotations;
        part pronunciationOverrides : PronunciationOverrides;
        part partOfSpeechAdapter : PartOfSpeechAdapter;
        part analysisDiagnostics : AnalysisDiagnostics;
    }
}
