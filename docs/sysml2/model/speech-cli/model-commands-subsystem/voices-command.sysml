package SpeechCli {
    part def VoicesCommand {
        doc /* Implements voices packs|install|uninstall: lists reference-voice packs, installs one
             * with mirror-aware progress and cancellation (emotion variants by default, base voices
             * only with --no-emotions), shows download and installed sizes, or removes it,
             * printing the consent note. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/VoicesCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/VoicesCommandTests.cs */
        comment designRef /* Design: docs/design/speech-cli/model-commands-subsystem/voices-command.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/model-commands-subsystem/voices-command.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/model-commands-subsystem/voices-command.yaml */
    }
}
