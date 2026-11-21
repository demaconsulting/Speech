package SpeechTextEn_CheckReport {
    private import SoftwareItemMetadata::*;

    part def CheckReport {
        doc /* Collector of the findings and information lines of the training data checks: writes them in recording
             * order with a per-check limit, writes the summary line, and decides the exit code (0 without a finding,
             * 1 with one). */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/CheckReport.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/CheckReportTests.cs");
            designFiles = ("docs/design/speech-text-en/training-data-subsystem/check-report.md");
            verificationFiles = ("docs/verification/speech-text-en/training-data-subsystem/check-report.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-data-subsystem/check-report.yaml");
        }
    }
}
