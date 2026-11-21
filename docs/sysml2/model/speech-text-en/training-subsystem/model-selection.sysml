package SpeechTextEn_ModelSelection {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_AveragedPerceptronTrainer::*;
    private import SpeechTextEn_DeterministicMath::*;
    private import SpeechTextEn_HeteronymFeatureExtractor::*;
    private import SpeechTextEn_ReadingInventory::*;
    private import SpeechTextEn_SparsePerceptronModel::*;
    private import SpeechTextEn_TrainingCorpus::*;
    private import SpeechTextEn_TrainingRecord::*;

    part def ModelSelection {
        doc /* Development-set choices and model construction: keeps the epoch with the most natural development rows
             * correct (earliest on ties), chooses the calibration temperature on a k/20 grid by the deterministic
             * negative log-likelihood (smaller k on ties), and builds the compressed and the uncompressed model image
             * whose header carries the inventory version, feature-set version, trainer version and data hash. Also
             * holds the TrainedModel and EpochScore values. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/ModelSelection.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/ModelSelectionTests.cs");
            designFiles = ("docs/design/speech-text-en/training-subsystem/model-selection.md");
            verificationFiles = ("docs/verification/speech-text-en/training-subsystem/model-selection.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-subsystem/model-selection.yaml");
        }

        // Trains over corpus epochs, picks the best epoch and calibration via deterministic math, and
        // builds the model.
        dependency ModelSelection to AveragedPerceptronTrainer;
        dependency ModelSelection to DeterministicMath;
        dependency ModelSelection to HeteronymFeatureExtractor;
        dependency ModelSelection to ReadingInventory;
        dependency ModelSelection to SparsePerceptronModel;
        dependency ModelSelection to TrainingCorpus;
        dependency ModelSelection to TrainingRecord;
    }
}
