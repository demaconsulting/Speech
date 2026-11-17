package SpeechTextEn {
    part def LeakageChecker {
        doc /* Leakage checks of the training data: always the separation of the committed splits (no
             * shared sentence, natural document or synthetic template) and, only when evaluation sets are
             * supplied, exact and near-duplicate overlap with them and the written per-set counts
             * (with the SentenceKeys helpers and the EvaluationSet record). */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/LeakageChecker.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/LeakageCheckerTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-data-subsystem/leakage-checker.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-data-subsystem/leakage-checker.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-data-subsystem/leakage-checker.yaml */
    }
}
