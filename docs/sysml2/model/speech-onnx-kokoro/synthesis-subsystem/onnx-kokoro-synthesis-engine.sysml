package SpeechOnnxKokoro {
    part def OnnxKokoroSynthesisEngine {
        doc /* Real ISynthesisBackend and IAnnotatedSynthesisBackend implementation running Kokoro v1.0's
             * bare ONNX graph directly through ONNX Runtime, confining every synthesis inference
             * interop call behind Speech's public mockable seam. It gets its phoneme token ids from the
             * KokoroPhonemePipeline unit (the plain Generate call speaks default readings without
             * normalization; GenerateAnnotated uses the annotations of the English text analysis), runs
             * the graph once per part of a long segment with the style row of each part, and joins the
             * audio. Constructed by OnnxKokoroEnglishSynthesisModel.CreateBackend. Also exposes a static
             * RunProbeInference method, sharing the engine's own input tensor name constants and a
             * vocabulary-verified in-vocabulary token id, used as OnnxExecutionProviderSelector's
             * validateSession probe to reject an accelerated execution provider that loads but cannot
             * actually run this model's graph. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.Kokoro/SynthesisSubsystem/OnnxKokoroSynthesisEngine.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.Kokoro.Tests/SynthesisSubsystem/OnnxKokoroSynthesisEngineTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-kokoro/synthesis-subsystem/onnx-kokoro-synthesis-engine.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-kokoro/synthesis-subsystem/onnx-kokoro-synthesis-engine.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-kokoro/synthesis-subsystem/onnx-kokoro-synthesis-engine.yaml */
    }
}
