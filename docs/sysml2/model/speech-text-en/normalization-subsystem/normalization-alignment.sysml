package SpeechTextEn {
    part def NormalizationAlignment {
        doc /* The alignment contract of the normalization: the public NormalizationKind (including
             * the host-override kind) and CliticKind enumerations and the internal token, span,
             * pronunciation mark, directive, segment, builder and result
             * types. The builder rejects any span or pronunciation mark that would break an
             * alignment invariant (overlap, wrong order, empty span, whitespace edge, surrogate cut,
             * a spelled span that is not a letter sequence), counts the overrides applied and
             * ignored, and the result
             * exposes the spans, the pronunciation marks, the identity gaps, the spoken text and the
             * reconciled sentence
             * breaks; it also reports whether the previous span directly touches a position and
             * ends with a letter. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/NormalizationAlignment.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/NormalizationAlignmentTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/normalization-subsystem/normalization-alignment.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/normalization-subsystem/normalization-alignment.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/normalization-subsystem/normalization-alignment.yaml */
    }
}
