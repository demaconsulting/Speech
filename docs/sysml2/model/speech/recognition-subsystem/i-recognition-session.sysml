package Speech_IRecognitionSession {
    private import SoftwareItemMetadata::*;
    private import Speech_RecognitionSessionState::*;
    private import Speech_SpeechRecognitionResult::*;

    part def IRecognitionSession {
        doc /* Layer 5 contract for one streaming recognition session bound to one capture device: a forward-only
             * RecognitionSessionState machine with a StateChanged event, StartAsync/StopAsync lifecycle control, and
             * GetResultsAsync, an async-enumerable, single-consumer stream of ordered provisional and final
             * SpeechRecognitionEvent results. Single-use: a session may not be restarted after Stopped. A session
             * fault surfaces as RecognitionSessionFaultedException from the active GetResultsAsync enumeration. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/IRecognitionSession.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionSessionTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/i-recognition-session.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/i-recognition-session.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/i-recognition-session.yaml");
        }

        // Publishes recognition results and session state changes.
        dependency IRecognitionSession to RecognitionSessionState;
        dependency IRecognitionSession to SpeechRecognitionResult;
    }
}
