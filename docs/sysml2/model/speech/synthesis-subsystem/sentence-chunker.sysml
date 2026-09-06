package Speech {
    part def SentenceChunker {
        doc /* Pure, stateless splitter that divides synthesis-ready narration text into
             * sentence/clause-sized chunks bounded by a configured maximum length, falling back
             * from primary sentence boundaries to secondary clause boundaries and then a
             * whitespace budget. A maximal run of consecutive primary sentence-ending characters
             * (e.g. an ellipsis "...", or a mixed terminator like "?!") is treated as a single
             * boundary, splitting once after the last character of the run rather than once per
             * character. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/SentenceChunker.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SentenceChunkerTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.md (documented with the pipeline it serves) */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.md (documented with the pipeline it serves) */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.yaml */
    }
}
