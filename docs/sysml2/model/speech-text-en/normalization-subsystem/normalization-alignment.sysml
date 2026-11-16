package SpeechTextEn {
    part def NormalizationAlignment {
        doc /* The alignment contract of the normalization: the public NormalizationKind and
             * CliticKind enumerations and the internal token, span, segment, builder and result
             * types. The builder rejects any span that would break an alignment invariant
             * (overlap, wrong order, empty span, whitespace edge, surrogate cut), and the result
             * exposes the spans, the identity gaps, the spoken text and the reconciled sentence
             * breaks; it also reports whether the previous span directly touches a position and
             * ends with a letter. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/NormalizationAlignment.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/NormalizationAlignmentTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/normalization-subsystem/normalization-alignment.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/normalization-subsystem/normalization-alignment.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/normalization-subsystem/normalization-alignment.yaml */
    }
}
