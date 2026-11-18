package SpeechTextEn {
    part def ExternalRowConversion {
        doc /* The conversion shared by the dataset adapters: UTF-8 byte offsets to UTF-16 character offsets
             * (ByteOffsets), the strict id-only mapping of dataset labels to reading identifiers
             * (ReadingIdMap), and the conversion of adapter records to evaluator rows with a count of every
             * skipped row by reason and of rows that equal a training or development sentence
             * (ExternalRowConverter). Also holds the ExternalRecord, ExternalReadResult, ConversionCounts,
             * ExternalRowSet and SkipReasons types. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/ExternalEvaluationSubsystem/ExternalRowConversion.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/ExternalRowConversionTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/external-evaluation-subsystem/external-row-conversion.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/external-evaluation-subsystem/external-row-conversion.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/external-evaluation-subsystem/external-row-conversion.yaml */
    }
}
