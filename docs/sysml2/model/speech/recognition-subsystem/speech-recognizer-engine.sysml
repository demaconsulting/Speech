package Speech_SpeechRecognizerEngine {
    private import SoftwareItemMetadata::*;
    private import Speech_IAudioCaptureDevice::*;
    private import Speech_IRecognitionSession::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_ISpeechRecognizerEngine::*;
    private import Speech_RecognitionBackend::*;
    private import Speech_RecognitionEngineBusyException::*;
    private import Speech_RecognitionSession::*;
    private import Speech_SpeechDiagnosticLevel::*;
    private import Speech_SpeechModelContract::*;
    private import Speech_UnavailableRecognitionSession::*;

    part def SpeechRecognizerEngine {
        doc /* Real ISpeechRecognizerEngine implementation: holds one loaded, "hot" IRecognitionBackend and a
             * single-slot exclusivity lease (SemaphoreSlim(1,1), fail-fast, no queueing), and constructs a new
             * RecognitionSession bound to one capture device per successful CreateSessionAsync call. Throws
             * RecognitionEngineBusyException rather than blocking when the lease is already held. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/SpeechRecognizerEngine.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SpeechRecognizerEngineTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/speech-recognizer-engine.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/speech-recognizer-engine.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/speech-recognizer-engine.yaml");
        }

        // Wraps a loaded recognition backend and hands out one exclusive recognition session at a time.
        dependency SpeechRecognizerEngine to IAudioCaptureDevice;
        dependency SpeechRecognizerEngine to IRecognitionSession;
        dependency SpeechRecognizerEngine to ISpeechDiagnostics;
        dependency SpeechRecognizerEngine to ISpeechRecognizerEngine;
        dependency SpeechRecognizerEngine to RecognitionBackend;
        dependency SpeechRecognizerEngine to RecognitionEngineBusyException;
        dependency SpeechRecognizerEngine to RecognitionSession;
        dependency SpeechRecognizerEngine to SpeechDiagnosticLevel;
        dependency SpeechRecognizerEngine to SpeechModelContract;
        dependency SpeechRecognizerEngine to UnavailableRecognitionSession;
    }
}
