package SpeechCli {
    part def ModelCommandsSubsystem {
        doc /* The five model-management subcommands (list-models, model-info, download,
             * uninstall, clean), plus the CLI-owned catalog seam
             * (ICliModelCatalog/SpeechModelCatalogAdapter/CliModelCatalogFactory) they all
             * share over the library's SpeechModelCatalog/SpeechModelStore. */

        comment designRef /* Design: docs/design/speech-cli/model-commands-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/model-commands-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/model-commands-subsystem.yaml */

        part iCliModelCatalog : ICliModelCatalog;
        part speechModelCatalogAdapter : SpeechModelCatalogAdapter;
        part cliModelCatalogFactory : CliModelCatalogFactory;
        part listModelsCommand : ListModelsCommand;
        part modelInfoCommand : ModelInfoCommand;
        part downloadCommand : DownloadCommand;
        part uninstallCommand : UninstallCommand;
        part cleanCommand : CleanCommand;
    }

    part def ICliModelCatalog {
        doc /* CLI-owned seam for enumerating known models, downloading, uninstalling, and
             * cleaning up leftovers by id, so every command can be tested against controlled
             * catalog data with no network access. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/ICliModelCatalog.cs */
    }

    part def SpeechModelCatalogAdapter {
        doc /* Real catalog seam implementation, owning and delegating to a real
             * SpeechModelCatalog (and, for uninstall/cleanup, its Store) without exposing the
             * library's internal-only constructor. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/SpeechModelCatalogAdapter.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/SpeechModelCatalogAdapterTests.cs */
    }

    part def CliModelCatalogFactory {
        doc /* Composes a SpeechModelCatalogAdapter per command invocation, honoring
             * --models-dir via SpeechModelStoreOptions.RootPathOverride. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/CliModelCatalogFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/SpeechModelCatalogAdapterTests.cs */
    }

    part def ListModelsCommand {
        doc /* Implements list-models: filters the catalog by --role/--state and prints an
             * aligned table or, with --format json, a JSON array. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/ListModelsCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/ListModelsCommandTests.cs */
    }

    part def ModelInfoCommand {
        doc /* Implements model-info: prints a known model's full identity and parameter
             * surface, or a clean error for an unrecognized model id. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/ModelInfoCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/ModelInfoCommandTests.cs */
    }

    part def DownloadCommand {
        doc /* Implements download: downloads one or more models sequentially, reports live
             * progress, isolates per-model failures, honors --force, and stops the whole
             * batch (rather than continuing) on a genuine Ctrl+C cancellation. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/DownloadCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/DownloadCommandTests.cs */
    }

    part def UninstallCommand {
        doc /* Implements uninstall: removes a model's installed files, treating a
             * never-installed model as a safe, successful no-op. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/UninstallCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/UninstallCommandTests.cs */
    }

    part def CleanCommand {
        doc /* Implements clean: removes any partial/stale download artifacts for a model,
             * never failing for a model with nothing to clean up. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/CleanCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/CleanCommandTests.cs */
    }
}
