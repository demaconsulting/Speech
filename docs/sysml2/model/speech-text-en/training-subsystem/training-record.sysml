package SpeechTextEn {
    part def TrainingRecord {
        doc /* The training and evaluation settings with their defaults and ranges, what the training read
             * (hashes, versions, row counts) and what it produced (choices, counts, image hash), and the
             * deterministic training.json text form that renders and parses them. Also holds the
             * TrainingSettings, EvaluationSettings, TrainingInputs and TrainingResult records. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/TrainingRecord.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/TrainingRecordTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-subsystem/training-record.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-subsystem/training-record.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-subsystem/training-record.yaml */
    }
}
