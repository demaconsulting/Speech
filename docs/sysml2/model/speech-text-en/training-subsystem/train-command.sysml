package SpeechTextEn {
    part def TrainCommand {
        doc /* Command line entry of the trainer's train command: trains the model from the training and
             * development data and writes the model file and the training record, or with --check verifies
             * that the committed model and record are what retraining produces. Experiment options need
             * --model-dir so the committed files cannot be overwritten. Exit codes: 0 trained or current,
             * 1 stale, 2 usage or input error. The Program.cs entry point only dispatches to this unit. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/TrainCommand.cs, tools/DemaConsulting.Speech.Text.En.Trainer/Program.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/TrainCommandTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-subsystem/train-command.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-subsystem/train-command.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-subsystem/train-command.yaml */
    }
}
