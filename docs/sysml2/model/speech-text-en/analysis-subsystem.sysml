package SpeechTextEn {
    part def AnalysisSubsystem {
        doc /* The English text analyzer (EnglishTextAnalyzer and its detailed-result types), its
             * options, the annotation vocabulary and typed reader (TextEnAnnotations), the internal
             * adapter that isolates the DemaConsulting.PartOfSpeech package (PartOfSpeechAdapter),
             * and the internal counts-only diagnostics reporter (AnalysisDiagnostics). */

        comment designRef /* Design: docs/design/speech-text-en/analysis-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/analysis-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/analysis-subsystem.yaml */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/AnalysisSubsystemTests.cs */

        part englishTextAnalyzer : EnglishTextAnalyzer;
        part englishTextAnalyzerOptions : EnglishTextAnalyzerOptions;
        part textEnAnnotations : TextEnAnnotations;
        part partOfSpeechAdapter : PartOfSpeechAdapter;
        part analysisDiagnostics : AnalysisDiagnostics;
    }
}
