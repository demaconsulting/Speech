package Speech {
    part def SentenceChunker {
        doc /* Pure, stateless splitter that divides synthesis-ready narration text into
             * sentence/clause-sized chunks: splits on primary sentence boundaries first, then
             * unconditionally splits every resulting piece further on secondary clause
             * boundaries (not gated by length), falling back to a whitespace budget only when a
             * piece still exceeds the configured maximum length. A maximal run of consecutive
             * primary sentence-ending characters (e.g. an ellipsis "...", or a mixed terminator
             * like "?!") is treated as a single boundary, splitting once after the last character
             * of the run rather than once per character. Primary/secondary boundary punctuation
             * embedded in a numeral is never treated as a boundary: a digit both before and
             * after (e.g. "12:30", "1,000", "0.5"), or a decimal point followed by a digit with
             * no digit and no letter before it (e.g. the leading "." in ".5" or "$.99"), stays
             * attached rather than splitting - a letter-preceded period (e.g. "Wait.5") does not
             * qualify and still splits normally. Any resulting piece with no letter or
             * digit content at all (e.g. a lone comma or a whitespace-spaced ellipsis like
             * ". . .") is merged onto the preceding chunk, or dropped if none precedes it.
             * ChunkWithMetadata additionally exposes, per chunk, whether it ends in a genuine
             * ellipsis, for a longer rendered pause there; Chunk remains a pure text-only
             * projection of ChunkWithMetadata. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/SentenceChunker.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SentenceChunkerTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.md (documented with the pipeline it serves) */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.md (documented with the pipeline it serves) */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.yaml */
    }
}
