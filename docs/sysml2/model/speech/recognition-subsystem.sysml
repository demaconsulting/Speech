package Speech {
    part def RecognitionSubsystem {
        doc /* Streaming speech-to-text: a Layer 3 engine contract bound to one loaded
             * recognition model, a Layer 5 session contract bound to one capture device, the
             * composition root that returns either a working engine or an honest unavailable
             * fallback, the capture-to-backend pipeline with its audio-format converter and
             * backpressure buffer, the cooperative-cancel-then-abandon native-call policy, and
             * the internal mockable speech-inference seam. */

        comment designRef /* Design: docs/design/speech/recognition-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem.yaml */

        part iSpeechRecognizerEngine : ISpeechRecognizerEngine;
        part iRecognitionSession : IRecognitionSession;
        part recognitionSessionState : RecognitionSessionState;
        part sessionStateChangedEventArgs : SessionStateChangedEventArgs;
        part recognitionSessionFaultedException : RecognitionSessionFaultedException;
        part recognitionEngineBusyException : RecognitionEngineBusyException;
        part speechRecognizerFactory : SpeechRecognizerFactory;
        part sherpaOnnxRecognitionSession : SherpaOnnxRecognitionSession;
        part sherpaOnnxSpeechRecognizerEngine : SherpaOnnxSpeechRecognizerEngine;
        part recognitionBackend : RecognitionBackend;
        part recognitionResultBuffer : RecognitionResultBuffer;
        part dedicatedWorker : DedicatedWorker;
        part audioFrameResampler : AudioFrameResampler;
        part unavailableSpeechRecognizerEngine : UnavailableSpeechRecognizerEngine;
        part unavailableRecognitionSession : UnavailableRecognitionSession;
        part speechRecognizerUnavailableException : SpeechRecognizerUnavailableException;
    }
}
