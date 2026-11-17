package SpeechTextEn {
    part def ReadingInventory {
        doc /* The public inventory of heteronym readings: loads the embedded, self-authored
             * readings and forms tables once, with strict validation, and answers which reading
             * ids exist (ReadingIds, Contains) and which readings an ASCII-letter word can carry
             * (CandidatesFor), compared with ASCII case folding and no culture dependency, in the
             * row order of the forms table. It carries an integer inventory revision (Version) and
             * owns the reading id grammar that the override validation and the typed annotation
             * reader share. It is immutable and thread-safe. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/HeteronymSubsystem/ReadingInventory.cs, data/text-en/readings.tsv, data/text-en/forms.tsv */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/HeteronymSubsystem/ReadingInventoryTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/heteronym-subsystem/reading-inventory.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/heteronym-subsystem/reading-inventory.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/heteronym-subsystem/reading-inventory.yaml */
    }
}
