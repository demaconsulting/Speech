package Speech_UnavailableRecognitionSession {
    private import SoftwareItemMetadata::*;
    private import Speech_IRecognitionSession::*;
    private import Speech_RecognitionSessionState::*;
    private import Speech_SpeechRecognitionResult::*;
    private import Speech_SpeechRecognizerUnavailableException::*;

    part def UnavailableRecognitionSession {
        doc /* Honest "unavailable" IRecognitionSession fallback returned by
             * UnavailableSpeechRecognizerEngine.CreateSessionAsync: reports IsAvailable as false and State as
             * Created, and throws SpeechRecognizerUnavailableException only when an operational member is invoked. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/UnavailableRecognitionSession.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/UnavailableRecognitionSessionTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/unavailable-recognition-session.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/unavailable-recognition-session.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/unavailable-recognition-session.yaml");
        }

        // Implements the session contract by throwing the recognizer-unavailable exception.
        dependency UnavailableRecognitionSession to IRecognitionSession;
        dependency UnavailableRecognitionSession to RecognitionSessionState;
        dependency UnavailableRecognitionSession to SpeechRecognitionResult;
        dependency UnavailableRecognitionSession to SpeechRecognizerUnavailableException;
    }
}
