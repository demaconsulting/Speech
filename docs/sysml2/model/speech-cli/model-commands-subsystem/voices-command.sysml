package SpeechCli {
    part def VoicesCommand {
        doc /* Implements voices packs|install|uninstall: lists reference-voice packs, installs one
             * with mirror-aware progress and cancellation (emotion variants by default, base voices
             * only with --no-emotions), shows download and installed sizes, or removes it,
             * printing the consent note. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/VoicesCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/VoicesCommandTests.cs */
    }
}
