package SpeechTextEn {
    part def CheckDataCommand {
        doc /* Command line entry of the trainer's check-data command: parses the options, finds the data
             * folder, runs the manifest, load, schema, license and leakage checks in a fixed order and
             * returns the exit code (0 pass, 1 findings, 2 usage or input error). The Program.cs entry
             * point is glue that dispatches to this unit. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/CheckDataCommand.cs, tools/DemaConsulting.Speech.Text.En.Trainer/Program.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/CheckDataCommandTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-data-subsystem/check-data-command.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-data-subsystem/check-data-command.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-data-subsystem/check-data-command.yaml */
    }
}
