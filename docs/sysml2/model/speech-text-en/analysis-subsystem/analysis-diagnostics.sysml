package SpeechTextEn_AnalysisDiagnostics {
    private import SoftwareItemMetadata::*;

    part def AnalysisDiagnostics {
        doc /* Internal reporter of one aggregate-counts diagnostics event per completed analysis (sentences, tokens,
             * annotations, unknown words, normalizations, ambiguous readings, numeric fallbacks, overrides applied
             * and overrides ignored), plus an optional second counts-only event when heteronym readings were assigned
             * (TextEn.Heteronym) and a one-time warning that a heteronym model is invalid or incompatible
             * (TextEn.Model; silent when there is no model source), built from fixed words and numbers only so no
             * text content and no dictionary content can reach the sink, and containing any failure of the sink. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/AnalysisDiagnostics.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/AnalysisDiagnosticsTests.cs");
            designFiles = ("docs/design/speech-text-en/analysis-subsystem/analysis-diagnostics.md");
            verificationFiles = ("docs/verification/speech-text-en/analysis-subsystem/analysis-diagnostics.md");
            requirementFiles = ("docs/reqstream/speech-text-en/analysis-subsystem/analysis-diagnostics.yaml");
        }

        // Reports aggregate analysis counts through the host diagnostics sink at the configured diagnostic
        // level.
        dependency AnalysisDiagnostics to Speech_ISpeechDiagnostics::ISpeechDiagnostics;
        dependency AnalysisDiagnostics to Speech_SpeechDiagnosticLevel::SpeechDiagnosticLevel;
    }
}
