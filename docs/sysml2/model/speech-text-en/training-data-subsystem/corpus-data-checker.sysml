package SpeechTextEn_CorpusDataChecker {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckReport::*;
    private import SpeechTextEn_TextEnDataSet::*;
    private import SpeechTextEn_TsvTable::*;

    part def CorpusDataChecker {
        doc /* The offline check of the corpus files on every check-data run: cluster-sources.tsv and
             * cluster-removals.tsv are both present or both absent; the source list has its schema, groups, statuses,
             * allowed mirror hosts, public-domain basis and pins (only used books are pinned); no used book is a
             * development or natural-holdout document (a leakage finding); and the removal list has its schema and
             * order. Its other findings use the check name corpus. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/CorpusDataChecker.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/CorpusDataCheckerTests.cs");
            designFiles = ("docs/design/speech-text-en/training-data-subsystem/corpus-data-checker.md");
            verificationFiles = ("docs/verification/speech-text-en/training-data-subsystem/corpus-data-checker.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-data-subsystem/corpus-data-checker.yaml");
        }

        // Reads the two corpus tables with their exact headers, compares the used books with the data set's
        // documents and reports violations to the check report.
        dependency CorpusDataChecker to CheckReport;
        dependency CorpusDataChecker to TextEnDataSet;
        dependency CorpusDataChecker to TsvTable;
    }
}
