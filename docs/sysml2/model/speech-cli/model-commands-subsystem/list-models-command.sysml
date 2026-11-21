package SpeechCli_ListModelsCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_CliModelCatalogFactory::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_ICliModelCatalog::*;

    enum def OutputFormat {
        enum Table;
        enum Json;
    }

    part def ListModelsCommand {
        doc /* Implements list-models: filters the catalog by --role/--state and prints an aligned table or, with
             * --format json, a JSON array. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/ListModelsCommand.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/ListModelsCommandTests.cs");
            designFiles = ("docs/design/speech-cli/model-commands-subsystem/list-models-command.md");
            verificationFiles = ("docs/verification/speech-cli/model-commands-subsystem/list-models-command.md");
            requirementFiles = ("docs/reqstream/speech-cli/model-commands-subsystem/list-models-command.yaml");
        }

        // Gets model descriptors from the catalog seam and filters them by role/state before printing.
        dependency ListModelsCommand to CliModelCatalogFactory;
        dependency ListModelsCommand to Context;
        dependency ListModelsCommand to ICliModelCatalog;
        dependency ListModelsCommand to Speech_SpeechModelDescriptor::SpeechModelDescriptor;
        dependency ListModelsCommand to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
    }
}
