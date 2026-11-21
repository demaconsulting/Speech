package SpeechCli_SpeakCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_AudioDeviceFactoryPlaybackDeviceSource::*;
    private import SpeechCli_CliModelCatalogFactory::*;
    private import SpeechCli_ConsoleCancellation::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_DevicesTestCommand::*;
    private import SpeechCli_ICliModelCatalog::*;
    private import SpeechCli_ICliPlaybackDeviceSource::*;
    private import SpeechCli_ParameterBagParser::*;

    part def SpeakCommand {
        doc /* Implements speak: resolves text from --text/--file/stdin (mutually exclusive), resolves and validates
             * the requested synthesis model, validates --tts-param values, strips audio tags when --no-tags is given,
             * dispatches to either a WAV file (--output-audio) or a real playback device (--playback-device/default),
             * loads an ISpeechSynthesizerEngine via ICliModelCatalog.CreateSynthesizerEngineAsync, binds an
             * ISynthesisSession to the resolved device, awaits session.SpeakAsync, and disposes the session, then the
             * engine, then the playback device (await using in declaration order, reversed) on every exit path. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/SynthesisCommandSubsystem/SpeakCommand.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/SpeakCommandTests.cs");
            designFiles = ("docs/design/speech-cli/synthesis-command-subsystem/speak-command.md");
            verificationFiles = ("docs/verification/speech-cli/synthesis-command-subsystem/speak-command.md");
            requirementFiles = ("docs/reqstream/speech-cli/synthesis-command-subsystem/speak-command.yaml");
        }

        // Gets a synthesizer from the catalog, parses tags and params, and plays audio to a device or WAV
        // file.
        dependency SpeakCommand to AudioDeviceFactoryPlaybackDeviceSource;
        dependency SpeakCommand to CliModelCatalogFactory;
        dependency SpeakCommand to ConsoleCancellation;
        dependency SpeakCommand to Context;
        dependency SpeakCommand to DevicesTestCommand;
        dependency SpeakCommand to ICliModelCatalog;
        dependency SpeakCommand to ICliPlaybackDeviceSource;
        dependency SpeakCommand to ParameterBagParser;
        dependency SpeakCommand to Speech_AudioDeviceFactory::AudioDeviceFactory;
        dependency SpeakCommand to Speech_AudioTagParser::AudioTagParser;
        dependency SpeakCommand to Speech_IAudioPlaybackDevice::IAudioPlaybackDevice;
        dependency SpeakCommand to Speech_SpeechModelDescriptor::SpeechModelDescriptor;
        dependency SpeakCommand to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency SpeakCommand to Speech_WavFileAudioPlaybackDevice::WavFileAudioPlaybackDevice;
    }
}
