package Speech_UnavailableSpeechRecognizerEngine {
    private import SoftwareItemMetadata::*;
    private import Speech_IAudioCaptureDevice::*;
    private import Speech_IRecognitionSession::*;
    private import Speech_ISpeechRecognizerEngine::*;
    private import Speech_UnavailableRecognitionSession::*;

    part def UnavailableSpeechRecognizerEngine {
        doc /* Honest "unavailable" ISpeechRecognizerEngine fallback: reports IsAvailable as false and
             * CreateSessionAsync returns UnavailableRecognitionSession.Instance rather than throwing for an ordinary
             * unavailable machine state. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/UnavailableSpeechRecognizerEngine.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/UnavailableSpeechRecognizerEngineTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/unavailable-speech-recognizer-engine.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/unavailable-speech-recognizer-engine.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/unavailable-speech-recognizer-engine.yaml");
        }

        // Implements the engine contract and returns only unavailable recognition sessions.
        dependency UnavailableSpeechRecognizerEngine to IAudioCaptureDevice;
        dependency UnavailableSpeechRecognizerEngine to IRecognitionSession;
        dependency UnavailableSpeechRecognizerEngine to ISpeechRecognizerEngine;
        dependency UnavailableSpeechRecognizerEngine to UnavailableRecognitionSession;
    }
}
