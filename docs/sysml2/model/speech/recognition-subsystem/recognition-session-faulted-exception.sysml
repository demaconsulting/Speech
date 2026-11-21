package Speech_RecognitionSessionFaultedException {
    private import SoftwareItemMetadata::*;

    part def RecognitionSessionFaultedException {
        doc /* Exception surfaced from an active IRecognitionSession.GetResultsAsync enumeration when the session
             * transitions to RecognitionSessionState.Faulted, wrapping the underlying cause (for example a capture
             * device becoming unavailable mid-session). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/RecognitionSessionFaultedException.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionSessionTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/recognition-session-faulted-exception.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/recognition-session-faulted-exception.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/recognition-session-faulted-exception.yaml");
        }
    }
}
