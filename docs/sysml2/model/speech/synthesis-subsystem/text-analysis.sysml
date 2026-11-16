package Speech {
    part def TextAnalysis {
        doc /* Language-neutral text-analysis seam: the optional ITextAnalyzer a synthesis model
             * supplies, which runs once per request over the plain text of the whole request
             * with each audio tag replaced by one opaque placeholder character, and returns
             * SegmentAnnotations (offset, length, opaque kind and value) plus optional
             * authoritative sentence boundaries; the internal mapper that builds the analysis
             * text and re-bases annotations and breaks onto the plain-text spans (dropping any
             * annotation it cannot place wholly inside one span, and carrying a sentence break
             * between consecutive tags on the preceding tag span); and the supporting
             * TextAnalysisResult, SegmentAnnotation and TextAnalysisConventions types. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/ITextAnalyzer.cs, TextAnalysisResult.cs, SegmentAnnotation.cs, TextAnalysisConventions.cs, TextAnalysisMapper.cs, AnnotationList.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/TextAnalysisMapperTests.cs, SegmentAnnotationTests.cs, TextAnalysisResultTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/text-analysis.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/text-analysis.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/text-analysis.yaml */
    }
}
