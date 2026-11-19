package SpeechOnnxKokoro {
    part def SynthesisSubsystem {
        doc /* The real ONNX-Runtime-backed implementation of Speech's public ISynthesisBackend
             * seam (and its annotated derivative) over Kokoro v1.0's bare ONNX graph, owning the
             * conversion of text to phoneme token ids entirely in managed code: the
             * KokoroPhonemePipeline unit (shared English phonemizer, Kokoro phoneme symbols,
             * token ids, parts for long text) and the OnnxKokoroSynthesisEngine unit (one graph run
             * per part). */

        comment designRef /* Design: docs/design/speech-onnx-kokoro/synthesis-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-kokoro/synthesis-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-kokoro/synthesis-subsystem.yaml */

        part kokoroPhonemePipeline : KokoroPhonemePipeline;
        part onnxKokoroSynthesisEngine : OnnxKokoroSynthesisEngine;
    }
}
