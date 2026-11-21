package Speech_SynthesisSessionFaultedException {
    private import SoftwareItemMetadata::*;

    part def SynthesisSessionFaultedException {
        doc /* Exception thrown when an operation is attempted on, or surfaced from, an ISynthesisSession that has
             * transitioned to SynthesisSessionState.Faulted (an in-flight SpeakAsync/SynthesizeAsync operation failed
             * for a reason other than its own requested cancellation, including a native call abandoned without ever
             * being requested to stop). Carries the original fault as its inner exception; the session is terminal
             * and must be disposed and replaced. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/SynthesisSessionFaultedException.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SynthesisSessionTests.cs", "test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SynthesisSessionFaultedExceptionTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/synthesis-session-faulted-exception.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/synthesis-session-faulted-exception.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/synthesis-session-faulted-exception.yaml");
        }
    }
}
