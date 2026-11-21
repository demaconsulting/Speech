package SpeechTextEn_LeakageChecker {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckReport::*;
    private import SpeechTextEn_TextEnDataSet::*;

    part def LeakageChecker {
        doc /* Leakage checks of the training data: always the separation of the committed splits (no shared sentence,
             * natural document or synthetic template) and, only when evaluation sets are supplied, exact and
             * near-duplicate overlap with them and the written per-set counts (with the SentenceKeys helpers and the
             * EvaluationSet record). */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/LeakageChecker.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/LeakageCheckerTests.cs");
            designFiles = ("docs/design/speech-text-en/training-data-subsystem/leakage-checker.md");
            verificationFiles = ("docs/verification/speech-text-en/training-data-subsystem/leakage-checker.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-data-subsystem/leakage-checker.yaml");
        }

        // Checks the loaded data set's splits for shared sentences and reports violations to the check
        // report.
        dependency LeakageChecker to CheckReport;
        dependency LeakageChecker to TextEnDataSet;
    }
}
