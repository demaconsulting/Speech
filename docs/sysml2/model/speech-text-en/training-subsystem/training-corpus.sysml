package SpeechTextEn {
    part def TrainingCorpus {
        doc /* Loader of the training data: opens exactly train.tsv, dev.tsv, readings.tsv and forms.tsv
             * (never a holdout file) and prepares each labeled row with the analyzer's own tokenizer, tagger,
             * token description and feature extractor, with the data hash and the feature-stream hash. Also
             * holds the LabeledRow and PreparedRow values and the RowPreparer reader. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/TrainingCorpus.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/TrainingCorpusTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-subsystem/training-corpus.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-subsystem/training-corpus.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-subsystem/training-corpus.yaml */
    }
}
