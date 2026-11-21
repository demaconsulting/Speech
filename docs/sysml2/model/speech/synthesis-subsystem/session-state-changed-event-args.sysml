package Speech_SynthesisSessionStateChangedEventArgs {
    private import SoftwareItemMetadata::*;
    private import Speech_SynthesisSessionState::*;

    part def SynthesisSessionStateChangedEventArgs {
        doc /* Event arguments record carrying the Previous/Current SynthesisSessionState of an
             * ISynthesisSession.StateChanged transition. Declared separately from RecognitionSubsystem's
             * identically-shaped record (which carries RecognitionSessionState instead) rather than shared, matching
             * this library's existing "no cross-subsystem public type" boundary. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/SessionStateChangedEventArgs.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/i-synthesis-session.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/i-synthesis-session.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/i-synthesis-session.yaml");
        }

        // Carries the previous and current synthesis session states.
        dependency SynthesisSessionStateChangedEventArgs to SynthesisSessionState;
    }
}
