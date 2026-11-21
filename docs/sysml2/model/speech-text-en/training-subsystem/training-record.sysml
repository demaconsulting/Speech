package SpeechTextEn_TrainingRecord {
    private import SoftwareItemMetadata::*;

    part def TrainingRecord {
        doc /* The training and evaluation settings with their defaults and ranges, what the training read (hashes,
             * versions, row counts) and what it produced (choices, counts, image hash), and the deterministic
             * training.json text form that renders and parses them. Also holds the TrainingSettings,
             * EvaluationSettings, TrainingInputs and TrainingResult records. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/TrainingRecord.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/TrainingRecordTests.cs");
            designFiles = ("docs/design/speech-text-en/training-subsystem/training-record.md");
            verificationFiles = ("docs/verification/speech-text-en/training-subsystem/training-record.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-subsystem/training-record.yaml");
        }
    }
}
