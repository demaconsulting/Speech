package SpeechTextEn {
    part def EnglishTextAnalyzerOptions {
        doc /* Immutable, validated options of an EnglishTextAnalyzer: the annotation families to
             * produce (the TextEnAnnotationSelection flags enumeration: part of speech and
             * normalization by default), an optional counts-only diagnostics sink and an optional
             * dictionary of pronunciation overrides (which requires normalization). */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/EnglishTextAnalyzerOptions.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/EnglishTextAnalyzerOptionsTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/analysis-subsystem/english-text-analyzer-options.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/analysis-subsystem/english-text-analyzer-options.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/analysis-subsystem/english-text-analyzer-options.yaml */
    }
}
