package Speech {
    part def SynthesisSubsystem {
        doc /* Text-to-speech: the closed, fixed Natural Language Audio Tag vocabulary and the
             * model-independent Layer 1 parser, plus Layer 2 per-model rendering, sentence
             * chunking, the public Layer 3/Layer 5 Engine/Session text-to-speech contract
             * (ISpeechSynthesizerEngine/ISynthesisSession) with its composition root and honest
             * unavailable fallbacks at every layer, engine exclusivity leasing, a
             * cancel-then-abandon dedicated worker, and the real sherpa-onnx synthesis backend
             * and playback-format converter. */

        comment designRef /* Design: docs/design/speech/synthesis-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem.yaml */

        part audioTagParser : AudioTagParser;
        part iSpeechSynthesizerEngine : ISpeechSynthesizerEngine;
        part iSynthesisSession : ISynthesisSession;
        part speechSynthesizerFactory : SpeechSynthesizerFactory;
        part sherpaOnnxSpeechSynthesizerEngine : SherpaOnnxSpeechSynthesizerEngine;
        part sherpaOnnxSynthesisSession : SherpaOnnxSynthesisSession;
        part synthesisBackend : SynthesisBackend;
        part layer2Rendering : Layer2Rendering;
        part sentenceChunker : SentenceChunker;
        part playbackAudioResampler : PlaybackAudioResampler;
        part unavailableSpeechSynthesizerEngine : UnavailableSpeechSynthesizerEngine;
        part unavailableSynthesisSession : UnavailableSynthesisSession;
        part speechSynthesizerUnavailableException : SpeechSynthesizerUnavailableException;
        part synthesisSessionState : SynthesisSessionState;
        part sessionStateChangedEventArgs : SessionStateChangedEventArgs;
        part synthesisEngineBusyException : SynthesisEngineBusyException;
        part synthesisSessionFaultedException : SynthesisSessionFaultedException;
        part dedicatedWorker : DedicatedWorker;
    }
}
