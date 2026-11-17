package SpeechTextEn {
    part def SpanValidator {
        doc /* Pure strict whole-word check of the target span of one labeled row: a non-empty range in
             * UTF-16 code units, no offset inside a surrogate pair, a text that is a listed form of
             * the row's reading, and neighbors that are not letters or numbers, judged by Unicode
             * scalars. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/SpanValidator.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/SpanValidatorTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-data-subsystem/span-validator.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-data-subsystem/span-validator.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-data-subsystem/span-validator.yaml */
    }
}
