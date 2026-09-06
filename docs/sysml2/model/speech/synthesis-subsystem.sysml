package Speech {
    part def SynthesisSubsystem {
        doc /* Text-to-speech: the closed, fixed Natural Language Audio Tag vocabulary and the
             * model-independent Layer 1 parser (Sub-phase 4a), plus Layer 2 per-model rendering,
             * sentence chunking, the public ISpeechSynthesizer streaming/playback pipeline with
             * its composition root and honest unavailable fallback, and the real sherpa-onnx
             * synthesis engine and playback-format converter (Sub-phase 4b). */

        comment designRef /* Design: docs/design/speech/synthesis-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem.yaml */

        part audioTagParser : AudioTagParser;
        part iSpeechSynthesizer : ISpeechSynthesizer;
        part speechSynthesizerFactory : SpeechSynthesizerFactory;
        part sherpaOnnxSpeechSynthesizer : SherpaOnnxSpeechSynthesizer;
        part synthesisEngine : SynthesisEngine;
        part layer2Rendering : Layer2Rendering;
        part sentenceChunker : SentenceChunker;
        part playbackAudioResampler : PlaybackAudioResampler;
        part unavailableSpeechSynthesizer : UnavailableSpeechSynthesizer;
        part speechSynthesizerUnavailableException : SpeechSynthesizerUnavailableException;
    }
}
