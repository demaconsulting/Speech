package SpeechTextEn {
    part def TextEnAnnotations {
        doc /* The annotation vocabulary of the English text analysis (six kind names, of which
             * only en.upos is produced by this release) and the typed reader that gives a backend
             * the part of speech out of an annotation without hand-parsing kind and value. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/TextEnAnnotations.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/TextEnAnnotationsTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/analysis-subsystem/text-en-annotations.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/analysis-subsystem/text-en-annotations.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/analysis-subsystem/text-en-annotations.yaml */
    }
}
