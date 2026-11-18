package SpeechOnnxKokoro {
    part def KokoroPhonemePipeline {
        doc /* Turns the text of one speech segment, with the annotations of the English text analysis
             * when there are any, into the phoneme token ids Kokoro v1.0 expects: phonemization by the
             * shared EnglishPhonemizer of SpeechPhonemesEn, rendering as Kokoro's phoneme symbols by the
             * MisakiRenderer (US profile, function words in their weak forms), and conversion through
             * KokoroPhonemeVocabulary. A segment with more tokens than the model accepts is split into
             * parts between words, preferably after a clause mark, and never truncated; text without a
             * sound gives no part. Immutable and thread-safe. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.Kokoro/SynthesisSubsystem/KokoroPhonemePipeline.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.Kokoro.Tests/SynthesisSubsystem/KokoroPhonemePipelineTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-kokoro/synthesis-subsystem/kokoro-phoneme-pipeline.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-kokoro/synthesis-subsystem/kokoro-phoneme-pipeline.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-kokoro/synthesis-subsystem/kokoro-phoneme-pipeline.yaml */
    }
}
