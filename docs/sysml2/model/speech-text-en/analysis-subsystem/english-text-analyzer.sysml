package SpeechTextEn {
    part def EnglishTextAnalyzer {
        doc /* The public ITextAnalyzer implementation: annotates the word tokens of a speech
             * request with their Universal Dependencies part of speech, annotates the spans the
             * Normalization subsystem finds with their spoken form (en.norm) or, for letter
             * sequences, as letters to spell (en.spell), applies the host's pronunciation overrides
             * (resolved by PronunciationOverrides; en.norm, en.spell, en.pron or nothing for a word),
             * and supplies
             * authoritative sentence breaks, with every offset indexing the exact analyzed text
             * and tag placeholders neutralized to same-length spaces. Also returns the detailed,
             * local-only EnglishTextAnalysis (with its AnalyzedToken and AnalyzedNormalization
             * records). */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/EnglishTextAnalyzer.cs, src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/EnglishTextAnalysis.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/EnglishTextAnalyzerTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/analysis-subsystem/english-text-analyzer.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/analysis-subsystem/english-text-analyzer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/analysis-subsystem/english-text-analyzer.yaml */
    }
}
