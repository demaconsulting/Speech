package SpeechTextEn_DataManifest {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckReport::*;
    private import SpeechTextEn_TsvTable::*;

    part def DataManifest {
        doc /* Verifier of the committed manifest of the training data: every listed file exists with the recorded
             * SHA-256, byte count and row count, every file of the folder is listed (except the manifest and the
             * local cache folders), and the entries are unique, sorted and in canonical form. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/DataManifest.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/DataManifestTests.cs");
            designFiles = ("docs/design/speech-text-en/training-data-subsystem/data-manifest.md");
            verificationFiles = ("docs/verification/speech-text-en/training-data-subsystem/data-manifest.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-data-subsystem/data-manifest.yaml");
        }

        // Reads the manifest table and reports any file hash, byte or row-count mismatches to the check
        // report.
        dependency DataManifest to CheckReport;
        dependency DataManifest to TsvTable;
    }
}
