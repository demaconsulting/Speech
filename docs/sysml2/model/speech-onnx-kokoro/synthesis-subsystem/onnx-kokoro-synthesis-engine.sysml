package SpeechOnnxKokoro {
    part def OnnxKokoroSynthesisEngine {
        doc /* Real ISynthesisBackend implementation running Kokoro v1.0's bare ONNX graph
             * directly through ONNX Runtime, confining every synthesis inference interop call
             * behind Speech's public mockable seam, and owning phonemization itself via
             * KokoroLexiconPhonemizer and KokoroPhonemeVocabulary. Constructed by
             * OnnxKokoroEnglishSynthesisModel.CreateBackend. Also exposes a static
             * RunProbeInference method, sharing Generate's own input tensor name constants and
             * a vocabulary-verified in-vocabulary token id, used as OnnxExecutionProviderSelector's
             * validateSession probe to reject an accelerated execution provider that loads but
             * cannot actually run this model's graph. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.Kokoro/SynthesisSubsystem/OnnxKokoroSynthesisEngine.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.Kokoro.Tests/SynthesisSubsystem/OnnxKokoroSynthesisEngineTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-kokoro/synthesis-subsystem/onnx-kokoro-synthesis-engine.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-kokoro/synthesis-subsystem/onnx-kokoro-synthesis-engine.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-kokoro/synthesis-subsystem/onnx-kokoro-synthesis-engine.yaml */
    }
}
