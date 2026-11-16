package SpeechTextEn {
    part def AnalysisDiagnostics {
        doc /* Internal reporter of one aggregate-counts diagnostics event per completed analysis
             * (sentences, tokens, annotations, unknown words, normalizations, ambiguous readings,
             * numeric fallbacks, overrides applied and overrides ignored), built from fixed words and
             * numbers only so no text content and no dictionary content can
             * reach the sink, and
             * containing any failure of the sink. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/AnalysisDiagnostics.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/AnalysisDiagnosticsTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/analysis-subsystem/analysis-diagnostics.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/analysis-subsystem/analysis-diagnostics.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/analysis-subsystem/analysis-diagnostics.yaml */
    }
}
