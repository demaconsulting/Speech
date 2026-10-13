package SpeechCli {
    part def ModelInfoCommand {
        doc /* Implements model-info: prints a known model's full identity and parameter
             * surface, or a clean error for an unrecognized model id. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/ModelInfoCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/ModelInfoCommandTests.cs */
        comment designRef /* Design: docs/design/speech-cli/model-commands-subsystem.md (documented within the subsystem doc; no dedicated unit doc) */
        comment verificationRef /* Verification: docs/verification/speech-cli/model-commands-subsystem.md (verified within the subsystem doc; no dedicated unit doc) */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/model-commands-subsystem.yaml (requirements held in the subsystem file; no dedicated unit file) */
    }
}
