package SpeechCli_CliModelCatalogFactory {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_SpeechModelCatalogAdapter::*;

    part def CliModelCatalogFactory {
        doc /* Composes a SpeechModelCatalogAdapter per command invocation, honoring --models-dir via
             * SpeechModelStoreOptions.RootPathOverride, and --mirror-url/
             * --mirror-user/--mirror-password/--mirror-bearer-token (via Context.CreateMirror()) wrapped in a
             * SpeechModelDownloaderOptions, and --voices-dir as the extra voices folder. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/CliModelCatalogFactory.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/SpeechModelCatalogAdapterTests.cs");
            designFiles = ("docs/design/speech-cli/model-commands-subsystem/cli-model-catalog-factory.md");
            verificationFiles = ("docs/verification/speech-cli/model-commands-subsystem/cli-model-catalog-factory.md");
            requirementFiles = ("docs/reqstream/speech-cli/model-commands-subsystem/cli-model-catalog-factory.yaml");
        }

        // Builds a catalog adapter for each run, applying store and downloader options from the Context.
        dependency CliModelCatalogFactory to Context;
        dependency CliModelCatalogFactory to SpeechModelCatalogAdapter;
        dependency CliModelCatalogFactory to Speech_SpeechModelDownloaderOptions::SpeechModelDownloaderOptions;
        dependency CliModelCatalogFactory to Speech_SpeechModelStoreOptions::SpeechModelStoreOptions;
    }
}
