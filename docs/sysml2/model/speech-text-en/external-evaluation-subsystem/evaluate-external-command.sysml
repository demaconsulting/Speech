package SpeechTextEn {
    part def EvaluateExternalCommand {
        doc /* Command line entry of the trainer's evaluate-external command: guards the cache location,
             * fetches only on request, verifies the pinned sources, converts and evaluates the requested
             * external sets, runs the seed study from the training and development data alone, prints only
             * counts and our own identifiers, and writes or verifies the external evaluation record. Exit
             * codes: 0 done or record current, 1 record stale, 2 usage, input, pin, hash or data error
             * (with no figures printed). The Program.cs entry point only dispatches to this unit. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/ExternalEvaluationSubsystem/EvaluateExternalCommand.cs, tools/DemaConsulting.Speech.Text.En.Trainer/Program.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/EvaluateExternalCommandTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/external-evaluation-subsystem/evaluate-external-command.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/external-evaluation-subsystem/evaluate-external-command.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/external-evaluation-subsystem/evaluate-external-command.yaml */
    }
}
