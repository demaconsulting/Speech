package Speech {
    part def AudioTagParser {
        doc /* Layer 1 of the two-layer tag rendering design: a pure, model-independent scanner
             * that recognizes closed-vocabulary Natural Language Audio Tag bracket syntax
             * (against the AudioTagCatalog alias table) and produces an ordered, neutral
             * sequence of TaggedTextSpan values. Groups the closed NaturalLanguageAudioTag /
             * NaturalLanguageAudioTagKind vocabulary and its AudioTagCatalog lookup table, since
             * neither has behavior independent of the parser that consumes them. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/AudioTagParser.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/AudioTagParserTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/audio-tag-parser.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/audio-tag-parser.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/audio-tag-parser.yaml */
    }
}
