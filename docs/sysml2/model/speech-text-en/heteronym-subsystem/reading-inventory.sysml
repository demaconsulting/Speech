package SpeechTextEn_ReadingInventory {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_HeteronymFeatureExtractor::*;

    part def ReadingInventory {
        doc /* The public inventory of heteronym readings: loads the embedded, self-authored readings and forms tables
             * once, with strict validation, and answers which reading ids exist (ReadingIds, Contains) and which
             * readings an ASCII-letter word can carry (CandidatesFor), compared with ASCII case folding and no
             * culture dependency, in the row order of the forms table. It carries an integer inventory revision
             * (Version) and owns the reading id grammar that the override validation and the typed annotation reader
             * share. It is immutable and thread-safe. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/HeteronymSubsystem/ReadingInventory.cs", "data/text-en/readings.tsv", "data/text-en/forms.tsv");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/HeteronymSubsystem/ReadingInventoryTests.cs");
            designFiles = ("docs/design/speech-text-en/heteronym-subsystem/reading-inventory.md");
            verificationFiles = ("docs/verification/speech-text-en/heteronym-subsystem/reading-inventory.md");
            requirementFiles = ("docs/reqstream/speech-text-en/heteronym-subsystem/reading-inventory.yaml");
        }

        // Uses the feature extractor's conventions to key readings and forms consistently with the
        // classifier.
        dependency ReadingInventory to HeteronymFeatureExtractor;
    }
}
