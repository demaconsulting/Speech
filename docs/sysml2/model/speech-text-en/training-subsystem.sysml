package SpeechTextEn {
    part def TrainingSubsystem {
        doc /* The train and evaluate commands of the non-shipped Text.En trainer: the corpus loader that
             * opens only the training, development and inventory files (TrainingCorpus), the deterministic
             * random generator and math (DeterministicRandom, DeterministicMath), the integer averaged
             * perceptron (AveragedPerceptronTrainer), the development-set epoch and temperature choices
             * and model images (ModelSelection), the training record (TrainingRecord) and its command
             * (TrainCommand), and the end-to-end evaluator with baselines, strata, clustered bootstrap,
             * quality gate and parity check (HeteronymEvaluator), the evaluation record (EvaluationRecord)
             * and its command (EvaluateCommand). It reuses the Text.En library's internal tokenization
             * adapter, feature extractor, scoring and model format through InternalsVisibleTo, so the
             * trained model matches what the analyzer computes. */

        comment designRef /* Design: docs/design/speech-text-en/training-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-subsystem.yaml */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystemTests.cs */

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
    }
}
