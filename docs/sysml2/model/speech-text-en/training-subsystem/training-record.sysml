package SpeechTextEn_TrainingRecord {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    attribute def TrainingSettings {
        attribute seed : Integer;
        attribute hashBits : Integer;
        attribute scale : Integer;
        attribute pruneThreshold : Integer;
        attribute naturalWeight : Integer;
        attribute maxEpochs : Integer;
        attribute temperatureSteps : Integer;
        attribute temperatureDivisor : Integer;
        attribute rowOrder : String;
    }

    attribute def GateThreshold {
        attribute set : String;
        attribute minAccuracyBasisPoints : Integer;
    }

    attribute def EvaluationSettings {
        attribute seeds : Integer [0..*];
        attribute resamples : Integer;
        attribute confidencePercent : Integer;
        attribute bootstrapSeed : Integer;
        attribute gateThresholds : GateThreshold [1..*];
    }

    attribute def TrainingInputs {
        attribute dataSha256 : String;
        attribute featuresSha256 : String;
        attribute taggerVersion : String;
        attribute featureSetVersion : Integer;
        attribute inventoryVersion : Integer;
        attribute trainerVersion : Integer;
        attribute trainRows : Integer;
        attribute trainUsed : Integer;
        attribute trainSkippedUnaligned : Integer;
        attribute trainSkippedSingleCandidate : Integer;
        attribute devRows : Integer;
        attribute devUsed : Integer;
        attribute devSkippedUnaligned : Integer;
        attribute devSkippedSingleCandidate : Integer;
    }

    attribute def TrainingResult {
        attribute selectedEpochs : Integer;
        attribute temperatureStep : Integer;
        attribute temperature : Real;
        attribute devNaturalCorrect : Integer;
        attribute devNaturalRows : Integer;
        attribute devAllCorrect : Integer;
        attribute devAllRows : Integer;
        attribute devNaturalNllMicro : Integer;
        attribute weightCount : Integer;
        attribute imageBytes : Integer;
        attribute imageSha256 : String;
    }

    part def TrainingRecord {
        doc /* The training and evaluation settings with their defaults and ranges, what the training read (hashes,
             * versions, row counts) and what it produced (choices, counts, image hash), and the deterministic
             * training.json text form (schema 2) that renders and parses them, including the per-set thresholds of the
             * statistical gate. Also holds the TrainingSettings, GateThreshold, EvaluationSettings, TrainingInputs and
             * TrainingResult records. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/TrainingRecord.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/TrainingRecordTests.cs");
            designFiles = ("docs/design/speech-text-en/training-subsystem/training-record.md");
            verificationFiles = ("docs/verification/speech-text-en/training-subsystem/training-record.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-subsystem/training-record.yaml");
        }

        attribute training : TrainingSettings;
        attribute evaluation : EvaluationSettings;
        attribute inputs : TrainingInputs;
        attribute result : TrainingResult;
    }
}
