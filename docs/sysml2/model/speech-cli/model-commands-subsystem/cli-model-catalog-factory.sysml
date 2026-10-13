package SpeechCli {
    part def CliModelCatalogFactory {
        doc /* Composes a SpeechModelCatalogAdapter per command invocation, honoring
             * --models-dir via SpeechModelStoreOptions.RootPathOverride, and --mirror-url/
             * --mirror-user/--mirror-password/--mirror-bearer-token (via Context.CreateMirror())
             * wrapped in a SpeechModelDownloaderOptions, and --voices-dir as the extra voices folder. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/CliModelCatalogFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/SpeechModelCatalogAdapterTests.cs */
        comment designRef /* Design: docs/design/speech-cli/model-commands-subsystem/cli-model-catalog-factory.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/model-commands-subsystem/cli-model-catalog-factory.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/model-commands-subsystem/cli-model-catalog-factory.yaml */
    }
}
