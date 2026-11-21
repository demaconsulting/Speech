package SpeechCli_SynthesisCommandSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_AudioDeviceFactoryPlaybackDeviceSource::*;
    private import SpeechCli_ICliPlaybackDeviceSource::*;
    private import SpeechCli_ParameterBagParser::*;
    private import SpeechCli_SpeakCommand::*;

    part def SynthesisCommandSubsystem {
        doc /* The one text-to-speech subcommand (speak), plus the ParameterBagParser --tts-param validation utility,
             * extending the ModelCommandsSubsystem's ICliModelCatalog seam with two further members
             * (GetPreferredAudioFormat, CreateSynthesizerEngineAsync) rather than introducing a second, competing
             * seam, and its own small ICliPlaybackDeviceSource seam over real playback-device resolution, letting
             * speak's device-dispatch logic be unit tested deterministically without depending on real PortAudio
             * hardware. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-cli/synthesis-command-subsystem.md");
            verificationFiles = ("docs/verification/speech-cli/synthesis-command-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-cli/synthesis-command-subsystem.yaml");
        }

        part speakCommand : SpeakCommand;
        part parameterBagParser : ParameterBagParser;
        part cliPlaybackDeviceSource : ICliPlaybackDeviceSource;
        part audioDeviceFactoryPlaybackDeviceSource : AudioDeviceFactoryPlaybackDeviceSource;
    }
}
