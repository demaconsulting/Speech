package SpeechTextEn {
    part def TextEnAnnotations {
        doc /* The annotation vocabulary of the English text analysis (six kind names:
             * en.upos, en.norm, en.spell, en.pron, en.reading and en.reading-confidence) and the
             * typed readers that give a backend the part of speech, the normalized or spelled spoken
             * text (as one replacement family), the pronunciation scheme and phonemes, or the
             * reading id and its confidence out of an annotation without hand-parsing kind and
             * value. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/TextEnAnnotations.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/TextEnAnnotationsTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/analysis-subsystem/text-en-annotations.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/analysis-subsystem/text-en-annotations.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/analysis-subsystem/text-en-annotations.yaml */
    }
}
