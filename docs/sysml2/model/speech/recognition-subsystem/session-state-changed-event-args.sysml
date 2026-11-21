package Speech_RecognitionSessionStateChangedEventArgs {
    private import SoftwareItemMetadata::*;
    private import Speech_RecognitionSessionState::*;

    part def RecognitionSessionStateChangedEventArgs {
        doc /* Immutable record payload for IRecognitionSession.StateChanged, carrying the Previous and Current
             * RecognitionSessionState values of one transition. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/SessionStateChangedEventArgs.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/i-recognition-session.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/i-recognition-session.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/i-recognition-session.yaml");
        }

        // Carries the previous and current recognition session states.
        dependency RecognitionSessionStateChangedEventArgs to RecognitionSessionState;
    }
}
