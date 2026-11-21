package Speech_RecognitionSubsystem {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioFrameResampler::*;
    private import Speech_IRecognitionSession::*;
    private import Speech_ISpeechRecognizerEngine::*;
    private import Speech_RecognitionBackend::*;
    private import Speech_RecognitionDedicatedWorker::*;
    private import Speech_RecognitionEngineBusyException::*;
    private import Speech_RecognitionResultBuffer::*;
    private import Speech_RecognitionSession::*;
    private import Speech_RecognitionSessionFaultedException::*;
    private import Speech_RecognitionSessionState::*;
    private import Speech_RecognitionSessionStateChangedEventArgs::*;
    private import Speech_SpeechRecognitionResult::*;
    private import Speech_SpeechRecognizerEngine::*;
    private import Speech_SpeechRecognizerFactory::*;
    private import Speech_SpeechRecognizerUnavailableException::*;
    private import Speech_UnavailableRecognitionSession::*;
    private import Speech_UnavailableSpeechRecognizerEngine::*;

    part def RecognitionSubsystem {
        doc /* Streaming speech-to-text: a Layer 3 engine contract bound to one loaded recognition model, a Layer 5
             * session contract bound to one capture device, the composition root that returns either a working engine
             * or an honest unavailable fallback, the capture-to-backend pipeline with its audio-format converter and
             * backpressure buffer, the cooperative-cancel-then-abandon native-call policy, and the public mockable
             * speech-inference seam. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionSubsystemTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem.yaml");
        }

        part iSpeechRecognizerEngine : ISpeechRecognizerEngine;
        part iRecognitionSession : IRecognitionSession;
        part recognitionSessionState : RecognitionSessionState;
        part sessionStateChangedEventArgs : RecognitionSessionStateChangedEventArgs;
        part recognitionSessionFaultedException : RecognitionSessionFaultedException;
        part recognitionEngineBusyException : RecognitionEngineBusyException;
        part speechRecognizerFactory : SpeechRecognizerFactory;
        part recognitionSession : RecognitionSession;
        part speechRecognizerEngine : SpeechRecognizerEngine;
        part recognitionBackend : RecognitionBackend;
        part recognitionResultBuffer : RecognitionResultBuffer;
        part dedicatedWorker : RecognitionDedicatedWorker;
        part audioFrameResampler : AudioFrameResampler;
        part unavailableSpeechRecognizerEngine : UnavailableSpeechRecognizerEngine;
        part unavailableRecognitionSession : UnavailableRecognitionSession;
        part speechRecognizerUnavailableException : SpeechRecognizerUnavailableException;
        part speechRecognitionResult : SpeechRecognitionResult;
    }
}
