package SpeechTextEn_LicenseChecker {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckReport::*;
    private import SpeechTextEn_DataManifest::*;
    private import SpeechTextEn_TextEnDataSet::*;

    part def LicenseChecker {
        doc /* License, attribution and excluded-material checks of the training data: the closed license allow-list,
             * the per-source license policy, restricted-license and Misaki terms, evaluation-only file names, the
             * attribution file and the license lines of the documents. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/LicenseChecker.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/LicenseCheckerTests.cs");
            designFiles = ("docs/design/speech-text-en/training-data-subsystem/license-checker.md");
            verificationFiles = ("docs/verification/speech-text-en/training-data-subsystem/license-checker.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-data-subsystem/license-checker.yaml");
        }

        // Checks data set sources against license policy and manifest files, reporting issues to the check
        // report.
        dependency LicenseChecker to CheckReport;
        dependency LicenseChecker to DataManifest;
        dependency LicenseChecker to TextEnDataSet;
    }
}
