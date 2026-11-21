package Speech_ISpeechRecognizerEngine {
    private import SoftwareItemMetadata::*;
    private import Speech_IAudioCaptureDevice::*;
    private import Speech_IRecognitionSession::*;

    part def ISpeechRecognizerEngine {
        doc /* Layer 3 contract for one loaded speech-inference engine bound to one recognition model: an availability
             * flag plus CreateSessionAsync, which leases exclusive use of the engine to a new IRecognitionSession for
             * one capture device. Exposes no inference-engine type. Never throws for an ordinary unavailable machine
             * state; throws RecognitionEngineBusyException only when a session is already leased. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/ISpeechRecognizerEngine.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SpeechRecognizerEngineTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/i-speech-recognizer-engine.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/i-speech-recognizer-engine.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/i-speech-recognizer-engine.yaml");
        }

        // Creates recognition sessions bound to a capture device.
        dependency ISpeechRecognizerEngine to IAudioCaptureDevice;
        dependency ISpeechRecognizerEngine to IRecognitionSession;
    }
}
