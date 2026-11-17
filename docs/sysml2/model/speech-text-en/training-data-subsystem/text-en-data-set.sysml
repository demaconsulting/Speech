package SpeechTextEn {
    part def TextEnDataSet {
        doc /* One-time loader of every table of the training data folder into typed rows that carry
             * their file and line (with the ReadingRecord, DataRow, ProvenanceRecord and SourceInfo
             * records), and owner of the closed vocabulary of data sources with each source's
             * allowed licenses and generated flag. A defect is a finding, never an exception. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/TextEnDataSet.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/TextEnDataSetTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-data-subsystem/text-en-data-set.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-data-subsystem/text-en-data-set.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-data-subsystem/text-en-data-set.yaml */
    }
}
