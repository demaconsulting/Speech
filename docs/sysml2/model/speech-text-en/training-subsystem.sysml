package SpeechTextEn_TrainingSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_AveragedPerceptronTrainer::*;
    private import SpeechTextEn_DeterministicMath::*;
    private import SpeechTextEn_DeterministicRandom::*;
    private import SpeechTextEn_EvaluateCommand::*;
    private import SpeechTextEn_EvaluationOverlay::*;
    private import SpeechTextEn_EvaluationRecord::*;
    private import SpeechTextEn_HeteronymEvaluator::*;
    private import SpeechTextEn_ModelSelection::*;
    private import SpeechTextEn_TrainCommand::*;
    private import SpeechTextEn_TrainingCorpus::*;
    private import SpeechTextEn_TrainingRecord::*;

    part def TrainingSubsystem {
        doc /* The train and evaluate commands of the non-shipped Text.En trainer: the corpus loader that opens only
             * the training, development and inventory files (TrainingCorpus), the deterministic random generator and
             * math (DeterministicRandom, DeterministicMath), the integer averaged perceptron
             * (AveragedPerceptronTrainer), the development-set epoch and temperature choices and model images
             * (ModelSelection), the training record (TrainingRecord) and its command (TrainCommand), and the
             * end-to-end evaluator with baselines, strata, clustered bootstrap, a statistical quality gate over four
             * scored sets with seed-derived thresholds, flip classification and parity check (HeteronymEvaluator),
             * the evaluation record with its gate rows and flip report (EvaluationRecord) and its command
             * (EvaluateCommand). It
             * reuses the Text.En library's internal tokenization adapter, feature extractor, scoring and model format
             * through InternalsVisibleTo, so the trained model matches what the analyzer computes. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystemTests.cs");
            designFiles = ("docs/design/speech-text-en/training-subsystem.md");
            verificationFiles = ("docs/verification/speech-text-en/training-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-subsystem.yaml");
        }

        part trainingCorpus : TrainingCorpus;
        part deterministicRandom : DeterministicRandom;
        part deterministicMath : DeterministicMath;
        part averagedPerceptronTrainer : AveragedPerceptronTrainer;
        part modelSelection : ModelSelection;
        part trainingRecord : TrainingRecord;
        part trainCommand : TrainCommand;
        part heteronymEvaluator : HeteronymEvaluator;
        part evaluationRecord : EvaluationRecord;
        part evaluateCommand : EvaluateCommand;
        part evaluationOverlay : EvaluationOverlay;
    }
}
