package Speech_SynthesisSessionState {
    private import SoftwareItemMetadata::*;

    enum def SynthesisSessionStateKind {
        enum Created;
        enum Starting;
        enum Running;
        enum Stopping;
        enum Stopped;
        enum Disposing;
        enum Disposed;
        enum Faulted;
    }

    part def SynthesisSessionState {
        doc /* The lifecycle states an ISynthesisSession passes through: Created, Starting, Running, Stopping,
             * Stopped, Disposing, Disposed, Faulted. Starting/Running/Stopping denote one discrete in-flight
             * SpeakAsync/SynthesizeAsync operation rather than a continuous stream - unlike recognition's continuous
             * capture window, a session returns to Stopped after each operation and is ready to accept another call.
             * Faulted is terminal. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/SynthesisSessionState.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/i-synthesis-session.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/i-synthesis-session.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/i-synthesis-session.yaml");
        }
    }
}
