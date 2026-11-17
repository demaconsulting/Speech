package SpeechTextEn {
    part def AveragedPerceptronTrainer {
        doc /* Candidate-restricted averaged perceptron over the hashed features in integer arithmetic:
             * per-feature-occurrence updates scored with the runtime's own scoring, a seeded shuffle with
             * natural rows repeated, exact averaging in 64-bit integers, and scaling, rounding, clamping
             * and pruning into deployable 16-bit weights after each epoch. Also holds the EpochWeights and
             * ShortWeights values. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/AveragedPerceptronTrainer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/AveragedPerceptronTrainerTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-subsystem/averaged-perceptron-trainer.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-subsystem/averaged-perceptron-trainer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-subsystem/averaged-perceptron-trainer.yaml */
    }
}
