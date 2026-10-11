package SpeechOnnxNemotronStt {
    part def RecognitionSubsystem {
        doc /* The real ONNX-Runtime-backed implementation of Speech's public IRecognitionBackend
             * seam over the Nemotron encoder, decoder, and joint graphs, together with the
             * streaming feature extractor, RNN-T greedy decoder, and leading-silence limiter and
             * dither units it composes. */

        comment designRef /* Design: docs/design/speech-onnx-nemotron-stt/recognition-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-nemotron-stt/recognition-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem.yaml */

        part onnxNemotronRecognitionEngine : OnnxNemotronRecognitionEngine;
        part nemotronFeatureExtractor : NemotronFeatureExtractor;
        part nemotronRnntGreedyDecoder : NemotronRnntGreedyDecoder;
        part silenceRunLimiter : SilenceRunLimiter;
    }
}
