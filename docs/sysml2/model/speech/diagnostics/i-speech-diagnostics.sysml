package Speech_ISpeechDiagnostics {
    private import SoftwareItemMetadata::*;
    private import Speech_SpeechDiagnosticLevel::*;

    part def ISpeechDiagnostics {
        doc /* Contract for reporting structural diagnostic events to an optional host-supplied sink, independent of
             * which library component raises the event. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/Diagnostics/ISpeechDiagnostics.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SpeechTests.cs");
            designFiles = ("docs/design/speech/diagnostics.md");
            verificationFiles = ("docs/verification/speech/diagnostics.md");
            requirementFiles = ("docs/reqstream/speech/diagnostics/i-speech-diagnostics.yaml");
        }

        // Classifies each reported event by diagnostic severity level.
        dependency ISpeechDiagnostics to SpeechDiagnosticLevel;
    }
}
