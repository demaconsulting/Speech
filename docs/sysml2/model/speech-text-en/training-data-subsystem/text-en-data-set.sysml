package SpeechTextEn_TextEnDataSet {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckReport::*;
    private import SpeechTextEn_DataManifest::*;
    private import SpeechTextEn_TsvTable::*;

    enum def SourceKind {
        enum Harvest;
        enum Synthetic;
        enum IndependentHoldout;
    }

    part def TextEnDataSet {
        doc /* One-time loader of every table of the training data folder into typed rows that carry their file and
             * line (with the ReadingRecord, DataRow, ProvenanceRecord and SourceInfo records), and owner of the
             * closed vocabulary of data sources with each source's allowed licenses and generated flag. A defect is a
             * finding, never an exception. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/TextEnDataSet.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/TextEnDataSetTests.cs");
            designFiles = ("docs/design/speech-text-en/training-data-subsystem/text-en-data-set.md");
            verificationFiles = ("docs/verification/speech-text-en/training-data-subsystem/text-en-data-set.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-data-subsystem/text-en-data-set.yaml");
        }

        // Loads every table with the strict TSV reader after manifest checks and reports load errors.
        dependency TextEnDataSet to CheckReport;
        dependency TextEnDataSet to DataManifest;
        dependency TextEnDataSet to TsvTable;
    }
}
