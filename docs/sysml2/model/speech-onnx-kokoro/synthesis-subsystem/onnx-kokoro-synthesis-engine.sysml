package SpeechOnnxKokoro {
    part def OnnxKokoroSynthesisEngine {
        doc /* Real ISynthesisBackend implementation running Kokoro v1.0's bare ONNX graph
             * directly through ONNX Runtime, confining every synthesis inference interop call
             * behind Speech's public mockable seam, and owning phonemization itself via
             * KokoroLexiconPhonemizer and KokoroPhonemeVocabulary. Constructed by
             * OnnxKokoroEnglishSynthesisModel.CreateBackend. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.Kokoro/SynthesisSubsystem/OnnxKokoroSynthesisEngine.cs */
        comment designRef /* Design: docs/design/speech-onnx-kokoro/synthesis-subsystem/onnx-kokoro-synthesis-engine.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-kokoro/synthesis-subsystem/onnx-kokoro-synthesis-engine.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-kokoro/synthesis-subsystem/onnx-kokoro-synthesis-engine.yaml */
    }
}
