package Speech_SpeechDiagnosticLevel {
    private import SoftwareItemMetadata::*;

    enum def SpeechDiagnosticLevelKind {
        enum Info;
        enum Warning;
        enum Error;
    }

    part def SpeechDiagnosticLevel {
        doc /* Supporting enumeration identifying the severity (informational, warning or error) of a structural fact
             * reported through ISpeechDiagnostics. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/Diagnostics/SpeechDiagnosticLevel.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/Diagnostics/NullSpeechDiagnosticsTests.cs");
            designFiles = ("docs/design/speech/diagnostics.md");
            verificationFiles = ("docs/verification/speech/diagnostics.md");
            requirementFiles = ("docs/reqstream/speech/diagnostics.yaml");
        }
    }
}
