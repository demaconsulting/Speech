package Speech_Diagnostics {
    private import SoftwareItemMetadata::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_NullSpeechDiagnostics::*;
    private import Speech_SpeechDiagnosticLevel::*;

    part def Diagnostics {
        doc /* Reports structural diagnostic events (e.g. device selected, fallback used) to an optional host-supplied
             * sink, carrying structural facts only, never raw audio or text. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Tests/Diagnostics/SpeechDiagnosticsTests.cs");
            designFiles = ("docs/design/speech/diagnostics.md");
            verificationFiles = ("docs/verification/speech/diagnostics.md");
            requirementFiles = ("docs/reqstream/speech/diagnostics.yaml");
        }

        part iSpeechDiagnostics : ISpeechDiagnostics;
        part nullSpeechDiagnostics : NullSpeechDiagnostics;
        part speechDiagnosticLevel : SpeechDiagnosticLevel;
    }
}
