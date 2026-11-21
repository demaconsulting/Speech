package SpeechCli_ModelInfoCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_CliModelCatalogFactory::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_ICliModelCatalog::*;

    part def ModelInfoCommand {
        doc /* Implements model-info: prints a known model's full identity and parameter surface, or a clean error for
             * an unrecognized model id. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/ModelInfoCommand.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/ModelInfoCommandTests.cs");
            designFiles = ("docs/design/speech-cli/model-commands-subsystem/model-info-command.md");
            verificationFiles = ("docs/verification/speech-cli/model-commands-subsystem/model-info-command.md");
            requirementFiles = ("docs/reqstream/speech-cli/model-commands-subsystem/model-info-command.yaml");
        }

        // Finds a model through the catalog seam, then prints its descriptor and declared parameters.
        dependency ModelInfoCommand to CliModelCatalogFactory;
        dependency ModelInfoCommand to Context;
        dependency ModelInfoCommand to ICliModelCatalog;
        dependency ModelInfoCommand to Speech_SpeechModelDescriptor::SpeechModelDescriptor;
        dependency ModelInfoCommand to Speech_SpeechModelParameters::SpeechModelParameters;
    }
}
