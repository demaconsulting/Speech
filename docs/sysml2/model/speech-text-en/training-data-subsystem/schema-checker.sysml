package SpeechTextEn {
    part def SchemaChecker {
        doc /* Structural and referential checks of the training data: the reading inventory (identifier
             * grammar, lemma, hints, status, descriptions, no phonetic characters), the mapping, forms
             * and extension decisions, every labeled row including its span, the provenance
             * identifiers, and the computed per-reading counts against the committed gap table. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/SchemaChecker.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/SchemaCheckerTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-data-subsystem/schema-checker.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-data-subsystem/schema-checker.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-data-subsystem/schema-checker.yaml */
    }
}
