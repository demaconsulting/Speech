package KokoroOnnxBenchmark_Program {
    private import SoftwareItemMetadata::*;

    part def Program {
        doc /* Top-level benchmark program: times Kokoro ONNX inference on the CPU or DirectML execution provider with
             * the Text.En analysis and the Phonemes.En phonemizer. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/KokoroOnnxBenchmark/Program.cs");
        }

        // Phonemizes via the Kokoro pipeline and vocabulary, then times OnnxKokoroSynthesisEngine on the
        // chosen provider.
        dependency Program to SpeechOnnxKokoro_KokoroPhonemePipeline::KokoroPhonemePipeline;
        dependency Program to SpeechOnnxKokoro_KokoroPhonemeVocabulary::KokoroPhonemeVocabulary;
        dependency Program to SpeechOnnxKokoro_OnnxKokoroSynthesisEngine::OnnxKokoroSynthesisEngine;
        dependency Program to SpeechOnnx_OnnxExecutionProviderSelector::OnnxExecutionProviderSelector;
    }
}
