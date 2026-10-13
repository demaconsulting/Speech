package SpeechCli {
    part def CleanCommand {
        doc /* Implements clean: removes any partial/stale download artifacts for a model,
             * never failing for a model with nothing to clean up. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/CleanCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/CleanCommandTests.cs */
        comment designRef /* Design: docs/design/speech-cli/model-commands-subsystem.md (documented within the subsystem doc; no dedicated unit doc) */
        comment verificationRef /* Verification: docs/verification/speech-cli/model-commands-subsystem.md (verified within the subsystem doc; no dedicated unit doc) */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/model-commands-subsystem.yaml (requirements held in the subsystem file; no dedicated unit file) */
    }
}
