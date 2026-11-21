package SpeechTextEn_SchemaChecker {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckReport::*;
    private import SpeechTextEn_SpanValidator::*;
    private import SpeechTextEn_TextEnDataSet::*;

    part def SchemaChecker {
        doc /* Structural and referential checks of the training data: the reading inventory (identifier grammar,
             * lemma, hints, status, descriptions, no phonetic characters), the mapping, forms and extension
             * decisions, every labeled row including its span, the provenance identifiers, and the computed
             * per-reading counts against the committed gap table. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/SchemaChecker.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/SchemaCheckerTests.cs");
            designFiles = ("docs/design/speech-text-en/training-data-subsystem/schema-checker.md");
            verificationFiles = ("docs/verification/speech-text-en/training-data-subsystem/schema-checker.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-data-subsystem/schema-checker.yaml");
        }

        // Validates the loaded data set's structure and spans with the span validator, reporting to the
        // check report.
        dependency SchemaChecker to CheckReport;
        dependency SchemaChecker to SpanValidator;
        dependency SchemaChecker to TextEnDataSet;
    }
}
