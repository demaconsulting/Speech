package Speech {
    part def RecognitionSubsystem {
        doc /* Streaming speech-to-text: the public recognizer contract and its result/event
             * types, the composition root that returns either a working recognizer or an honest
             * unavailable fallback, the capture-to-engine pipeline with its audio-format
             * converter, and the internal mockable speech-inference seam. */

        comment designRef /* Design: docs/design/speech/recognition-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem.yaml */

        part iSpeechRecognizer : ISpeechRecognizer;
        part speechRecognizerFactory : SpeechRecognizerFactory;
        part sherpaOnnxSpeechRecognizer : SherpaOnnxSpeechRecognizer;
        part recognitionEngine : RecognitionEngine;
        part audioFrameResampler : AudioFrameResampler;
        part unavailableSpeechRecognizer : UnavailableSpeechRecognizer;
        part speechRecognizerUnavailableException : SpeechRecognizerUnavailableException;
    }
}
