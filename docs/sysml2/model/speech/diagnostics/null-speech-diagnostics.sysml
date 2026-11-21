package Speech_NullSpeechDiagnostics {
    private import SoftwareItemMetadata::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_SpeechDiagnosticLevel::*;

    part def NullSpeechDiagnostics {
        doc /* Default no-op ISpeechDiagnostics sink that discards every event without throwing, used when a host
             * supplies no diagnostics implementation. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/Diagnostics/NullSpeechDiagnostics.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/Diagnostics/NullSpeechDiagnosticsTests.cs");
            designFiles = ("docs/design/speech/diagnostics.md");
            verificationFiles = ("docs/verification/speech/diagnostics.md");
            requirementFiles = ("docs/reqstream/speech/diagnostics/null-speech-diagnostics.yaml");
        }

        // Implements the diagnostics contract as a no-op sink that ignores events at every level.
        dependency NullSpeechDiagnostics to ISpeechDiagnostics;
        dependency NullSpeechDiagnostics to SpeechDiagnosticLevel;
    }
}
