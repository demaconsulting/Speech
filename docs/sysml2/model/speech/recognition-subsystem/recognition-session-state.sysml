package Speech_RecognitionSessionState {
    private import SoftwareItemMetadata::*;

    enum def RecognitionSessionStateKind {
        enum Created;
        enum Starting;
        enum Running;
        enum Stopping;
        enum Stopped;
        enum Disposing;
        enum Disposed;
        enum Faulted;
    }

    part def RecognitionSessionState {
        doc /* Forward-only IRecognitionSession lifecycle enumeration: Created, Starting, Running, Stopping, Stopped,
             * Disposing, Disposed, Faulted. Faulted is reachable from Starting, Running, or Stopping; no other
             * backward or skipped transition is valid. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/RecognitionSessionState.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/i-recognition-session.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/i-recognition-session.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/i-recognition-session.yaml");
        }
    }
}
