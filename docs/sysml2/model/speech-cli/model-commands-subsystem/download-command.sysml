package SpeechCli {
    part def DownloadCommand {
        doc /* Implements download: downloads one or more models sequentially, reports live
             * progress, isolates per-model failures, honors --force, and stops the whole
             * batch (rather than continuing) on a genuine Ctrl+C cancellation. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/DownloadCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/DownloadCommandTests.cs */
        comment designRef /* Design: docs/design/speech-cli/model-commands-subsystem/download-command.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/model-commands-subsystem/download-command.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/model-commands-subsystem/download-command.yaml */
    }
}
