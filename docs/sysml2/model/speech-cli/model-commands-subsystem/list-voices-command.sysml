package SpeechCli_ListVoicesCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_CliModelCatalogFactory::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_ICliModelCatalog::*;

    part def ListVoicesCommand {
        doc /* Implements list-voices: prints every available reference voice as a table or, with --format json, a
             * JSON array (with a Moods column / moods array of emotion variants), plus how to choose a voice and the
             * consent note. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/ListVoicesCommand.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/ListVoicesCommandTests.cs");
            designFiles = ("docs/design/speech-cli/model-commands-subsystem/list-voices-command.md");
            verificationFiles = ("docs/verification/speech-cli/model-commands-subsystem/list-voices-command.md");
            requirementFiles = ("docs/reqstream/speech-cli/model-commands-subsystem/list-voices-command.yaml");
        }

        // Lists voices from the reference voice library, fetched via the catalog seam, with moods per
        // voice.
        dependency ListVoicesCommand to CliModelCatalogFactory;
        dependency ListVoicesCommand to Context;
        dependency ListVoicesCommand to ICliModelCatalog;
        dependency ListVoicesCommand to Speech_ReferenceVoiceLibrary::ReferenceVoiceLibrary;
        dependency ListVoicesCommand to Speech_ReferenceVoicePack::ReferenceVoicePack;
    }
}
