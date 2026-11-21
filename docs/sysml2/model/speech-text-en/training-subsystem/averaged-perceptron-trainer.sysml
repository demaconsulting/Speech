package SpeechTextEn_AveragedPerceptronTrainer {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_DeterministicRandom::*;
    private import SpeechTextEn_HeteronymClassifier::*;
    private import SpeechTextEn_HeteronymFeatureExtractor::*;
    private import SpeechTextEn_ReadingInventory::*;
    private import SpeechTextEn_TrainingCorpus::*;
    private import SpeechTextEn_TrainingRecord::*;

    part def AveragedPerceptronTrainer {
        doc /* Candidate-restricted averaged perceptron over the hashed features in integer arithmetic:
             * per-feature-occurrence updates scored with the runtime's own scoring, a seeded shuffle with natural
             * rows repeated, exact averaging in 64-bit integers, and scaling, rounding, clamping and pruning into
             * deployable 16-bit weights after each epoch. Also holds the EpochWeights and ShortWeights values. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/AveragedPerceptronTrainer.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/AveragedPerceptronTrainerTests.cs");
            designFiles = ("docs/design/speech-text-en/training-subsystem/averaged-perceptron-trainer.md");
            verificationFiles = ("docs/verification/speech-text-en/training-subsystem/averaged-perceptron-trainer.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-subsystem/averaged-perceptron-trainer.yaml");
        }

        // Trains on corpus rows with runtime features and classifier scoring, using seeded randomness for
        // determinism.
        dependency AveragedPerceptronTrainer to DeterministicRandom;
        dependency AveragedPerceptronTrainer to HeteronymClassifier;
        dependency AveragedPerceptronTrainer to HeteronymFeatureExtractor;
        dependency AveragedPerceptronTrainer to ReadingInventory;
        dependency AveragedPerceptronTrainer to TrainingCorpus;
        dependency AveragedPerceptronTrainer to TrainingRecord;
    }
}
