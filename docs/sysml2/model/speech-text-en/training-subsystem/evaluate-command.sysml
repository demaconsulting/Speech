package SpeechTextEn {
    part def EvaluateCommand {
        doc /* Command line entry of the trainer's evaluate command: evaluates the model on the development
             * and holdout sets, decides the quality gate on the natural holdout, runs the multi-seed study,
             * and writes or verifies the evaluation record. A holdout file is read only when the gate, an
             * explicit holdout set or the record asks for it. Exit codes: 0 done or gate passed, 1 gate
             * failed or record stale, 2 usage or input error. The Program.cs entry point only dispatches
             * to this unit. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/EvaluateCommand.cs, tools/DemaConsulting.Speech.Text.En.Trainer/Program.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/EvaluateCommandTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-subsystem/evaluate-command.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-subsystem/evaluate-command.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-subsystem/evaluate-command.yaml */
    }
}
