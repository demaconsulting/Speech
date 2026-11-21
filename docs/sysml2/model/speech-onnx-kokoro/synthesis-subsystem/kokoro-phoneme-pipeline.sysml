package SpeechOnnxKokoro_KokoroPhonemePipeline {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxKokoro_KokoroPhonemeVocabulary::*;

    part def KokoroPhonemePipeline {
        doc /* Turns the text of one speech segment, with the annotations of the English text analysis when there are
             * any, into the phoneme token ids Kokoro v1.0 expects: phonemization by the shared EnglishPhonemizer of
             * SpeechPhonemesEn, rendering as Kokoro's phoneme symbols by the MisakiRenderer (US profile, function
             * words in their weak forms), and conversion through KokoroPhonemeVocabulary. A segment with more tokens
             * than the model accepts is split into parts between words, preferably after a clause mark, and never
             * truncated; text without a sound gives no part. Immutable and thread-safe. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.Kokoro/SynthesisSubsystem/KokoroPhonemePipeline.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.Kokoro.Tests/SynthesisSubsystem/KokoroPhonemePipelineTests.cs");
            designFiles = ("docs/design/speech-onnx-kokoro/synthesis-subsystem/kokoro-phoneme-pipeline.md");
            verificationFiles = ("docs/verification/speech-onnx-kokoro/synthesis-subsystem/kokoro-phoneme-pipeline.md");
            requirementFiles = ("docs/reqstream/speech-onnx-kokoro/synthesis-subsystem/kokoro-phoneme-pipeline.yaml");
        }

        // Phonemizes text with the English phonemizer and Misaki renderer, then maps symbols to Kokoro
        // token ids.
        dependency KokoroPhonemePipeline to KokoroPhonemeVocabulary;
        dependency KokoroPhonemePipeline to SpeechPhonemesEn_EnglishPhonemizer::EnglishPhonemizer;
        dependency KokoroPhonemePipeline to SpeechPhonemesEn_MisakiRenderer::MisakiRenderer;
        dependency KokoroPhonemePipeline to Speech_TextAnalysis::TextAnalysis;
    }
}
