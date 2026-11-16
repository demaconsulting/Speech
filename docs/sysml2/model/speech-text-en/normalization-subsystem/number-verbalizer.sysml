package SpeechTextEn {
    part def NumberVerbalizer {
        doc /* Internal, stateless builder of the English words of a number: cardinals, ordinals,
             * years, decades, decimals, digit sequences, fractions, clock times and calendar dates,
             * written as lowercase words separated by single spaces without hyphens, commas or
             * digits, over a bounded stack buffer (at most 1,024 characters). It reads ASCII digits
             * only, validates its input and enforces documented size limits, and uses no
             * culture-sensitive API, so its result never depends on the current culture. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/NumberVerbalizer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/NumberVerbalizerTests.cs, test/DemaConsulting.Speech.Text.En.Tests/Fakes/ReferenceNumberWords.cs */
        comment designRef /* Design: docs/design/speech-text-en/normalization-subsystem/number-verbalizer.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/normalization-subsystem/number-verbalizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/normalization-subsystem/number-verbalizer.yaml */
    }
}
