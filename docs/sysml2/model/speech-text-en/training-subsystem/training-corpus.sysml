package SpeechTextEn_TrainingCorpus {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckReport::*;
    private import SpeechTextEn_EnglishTextAnalyzer::*;
    private import SpeechTextEn_HeteronymFeatureExtractor::*;
    private import SpeechTextEn_PartOfSpeechAdapter::*;
    private import SpeechTextEn_ReadingInventory::*;
    private import SpeechTextEn_TextEnDataSet::*;
    private import SpeechTextEn_TsvTable::*;

    part def TrainingCorpus {
        doc /* Loader of the training data: opens exactly train.tsv, dev.tsv, readings.tsv and forms.tsv (never a
             * holdout file) and prepares each labeled row with the analyzer's own tokenizer, tagger, token
             * description and feature extractor, with the data hash and the feature-stream hash. Also holds the
             * LabeledRow and PreparedRow values and the RowPreparer reader. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/TrainingCorpus.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/TrainingCorpusTests.cs");
            designFiles = ("docs/design/speech-text-en/training-subsystem/training-corpus.md");
            verificationFiles = ("docs/verification/speech-text-en/training-subsystem/training-corpus.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-subsystem/training-corpus.yaml");
        }

        // Loads data tables and prepares rows with PartOfSpeech tags and heteronym features against the
        // inventory.
        dependency TrainingCorpus to CheckReport;
        dependency TrainingCorpus to EnglishTextAnalyzer;
        dependency TrainingCorpus to HeteronymFeatureExtractor;
        dependency TrainingCorpus to PartOfSpeechAdapter;
        dependency TrainingCorpus to ReadingInventory;
        dependency TrainingCorpus to TextEnDataSet;
        dependency TrainingCorpus to TsvTable;
        dependency TrainingCorpus to OtsDependencies::PartOfSpeech;
    }
}
