package Speech_TextAnalysis {
    private import SoftwareItemMetadata::*;

    part def TextAnalysis {
        doc /* Language-neutral text-analysis seam: the optional ITextAnalyzer a synthesis model supplies, which runs
             * once per request over the plain text of the whole request with each audio tag replaced by one opaque
             * placeholder character, and returns SegmentAnnotations (offset, length, opaque kind and value) plus
             * optional authoritative sentence boundaries; the internal mapper that builds the analysis text and
             * re-bases annotations and breaks onto the plain-text spans (dropping any annotation it cannot place
             * wholly inside one span, and carrying a sentence break between consecutive tags on the preceding tag
             * span); and the supporting TextAnalysisResult, SegmentAnnotation and TextAnalysisConventions types. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/ITextAnalyzer.cs", "src/DemaConsulting.Speech/SynthesisSubsystem/TextAnalysisResult.cs", "src/DemaConsulting.Speech/SynthesisSubsystem/SegmentAnnotation.cs", "src/DemaConsulting.Speech/SynthesisSubsystem/AnnotationList.cs", "src/DemaConsulting.Speech/SynthesisSubsystem/TextAnalysisConventions.cs", "src/DemaConsulting.Speech/SynthesisSubsystem/TextAnalysisMapper.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/TextAnalysisMapperTests.cs", "test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SegmentAnnotationTests.cs", "test/DemaConsulting.Speech.Tests/SynthesisSubsystem/TextAnalysisResultTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/text-analysis.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/text-analysis.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/text-analysis.yaml");
        }

        // Uses the audio tag parser to keep tags intact while analyzing text.
        dependency TextAnalysis to Speech_AudioTagParser::AudioTagParser;
    }
}
