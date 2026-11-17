package SpeechTextEn {
    part def TsvTable {
        doc /* Strict reader of one tab-separated data file: valid UTF-8, no byte order mark, no carriage
             * return, final newline, no empty line, constant column count and normalization form C,
             * split on tab only with no quoting, with the expected header and optional comment
             * lines, reporting findings with file and line. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/TsvTable.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/TsvTableTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-data-subsystem/tsv-table.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-data-subsystem/tsv-table.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-data-subsystem/tsv-table.yaml */
    }
}
