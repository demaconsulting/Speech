package SpeechTextEn_TsvTable {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckReport::*;

    part def TsvTable {
        doc /* Strict reader of one tab-separated data file: valid UTF-8, no byte order mark, no carriage return,
             * final newline, no empty line, constant column count and normalization form C, split on tab only with no
             * quoting, with the expected header and optional comment lines, reporting findings with file and line. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/TsvTable.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/TsvTableTests.cs");
            designFiles = ("docs/design/speech-text-en/training-data-subsystem/tsv-table.md");
            verificationFiles = ("docs/verification/speech-text-en/training-data-subsystem/tsv-table.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-data-subsystem/tsv-table.yaml");
        }

        // Reports file format violations, with file and line, to the check report.
        dependency TsvTable to CheckReport;
    }
}
