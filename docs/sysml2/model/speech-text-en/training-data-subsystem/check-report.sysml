package SpeechTextEn {
    part def CheckReport {
        doc /* Collector of the findings and information lines of the training data checks: writes them
             * in recording order with a per-check limit, writes the summary line, and decides the
             * exit code (0 without a finding, 1 with one). */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/CheckReport.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/CheckReportTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-data-subsystem/check-report.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-data-subsystem/check-report.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-data-subsystem/check-report.yaml */
    }
}
