package SpeechTextEn {
    part def EnglishTextAnalyzerOptions {
        doc /* Immutable, validated options of an EnglishTextAnalyzer: the annotation families to
             * produce (the TextEnAnnotationSelection flags enumeration: part of speech,
             * normalization and reading by default), an optional counts-only diagnostics sink, an optional
             * dictionary of pronunciation overrides (which requires normalization) and the confidence
             * below which a classified reading counts as low confidence. The Reading selection flag is
             * part of the default selection; it is effective only for words that carry a reading
             * override until a heteronym model ships. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/EnglishTextAnalyzerOptions.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/EnglishTextAnalyzerOptionsTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/analysis-subsystem/english-text-analyzer-options.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/analysis-subsystem/english-text-analyzer-options.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/analysis-subsystem/english-text-analyzer-options.yaml */
    }
}
