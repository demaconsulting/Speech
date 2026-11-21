package SpeechTextEn_SpanValidator {
    private import SoftwareItemMetadata::*;

    part def SpanValidator {
        doc /* Pure strict whole-word check of the target span of one labeled row: a non-empty range in UTF-16 code
             * units, no offset inside a surrogate pair, a text that is a listed form of the row's reading, and
             * neighbors that are not letters or numbers, judged by Unicode scalars. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/SpanValidator.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/SpanValidatorTests.cs");
            designFiles = ("docs/design/speech-text-en/training-data-subsystem/span-validator.md");
            verificationFiles = ("docs/verification/speech-text-en/training-data-subsystem/span-validator.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-data-subsystem/span-validator.yaml");
        }
    }
}
