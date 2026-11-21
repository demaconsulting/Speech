package Speech_AudioTagParser {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioTagDescriptor::*;
    private import Speech_TextAnalysis::*;

    enum def TaggedTextSpanKind {
        enum PlainText;
        enum Tag;
    }

    part def AudioTagParser {
        doc /* Layer 1 of the two-layer tag rendering design: a pure, model-independent scanner that recognizes
             * closed-vocabulary Natural Language Audio Tag bracket syntax (against the AudioTagCatalog alias table)
             * and produces an ordered, neutral sequence of TaggedTextSpan values. Groups the closed
             * NaturalLanguageAudioTag / NaturalLanguageAudioTagKind vocabulary and its AudioTagCatalog lookup table,
             * since neither has behavior independent of the parser that consumes them. A plain-text TaggedTextSpan
             * can additionally carry optional SegmentAnnotations and sentence breaks (relative to the span text,
             * defaulting to none) that a text analysis attaches; a tag span can carry sentence breaks (0 before, 1
             * after the tag) for a break between consecutive tags. The parser itself never produces them. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/AudioTagParser.cs", "src/DemaConsulting.Speech/SynthesisSubsystem/TaggedTextSpan.cs", "src/DemaConsulting.Speech/SynthesisSubsystem/TaggedTextSpanKind.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/AudioTagParserTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/audio-tag-parser.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/audio-tag-parser.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/audio-tag-parser.yaml");
        }

        // Matches tags against tag descriptors and outputs text analysis segments.
        dependency AudioTagParser to AudioTagDescriptor;
        dependency AudioTagParser to TextAnalysis;
    }
}
