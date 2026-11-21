package SpeechCli_AudioDeviceFactoryPlaybackDeviceSource {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_ICliPlaybackDeviceSource::*;

    part def AudioDeviceFactoryPlaybackDeviceSource {
        doc /* Production ICliPlaybackDeviceSource implementation, forwarding every call unchanged to a real, composed
             * AudioDeviceFactory - including its hardware-detection behavior - so SpeakCommand resolves real playback
             * devices exactly as the library does. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/SynthesisCommandSubsystem/AudioDeviceFactoryPlaybackDeviceSource.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/AudioDeviceFactoryPlaybackDeviceSourceTests.cs");
            designFiles = ("docs/design/speech-cli/synthesis-command-subsystem/audio-device-factory-playback-device-source.md");
            verificationFiles = ("docs/verification/speech-cli/synthesis-command-subsystem/audio-device-factory-playback-device-source.md");
            requirementFiles = ("docs/reqstream/speech-cli/synthesis-command-subsystem/audio-device-factory-playback-device-source.yaml");
        }

        // Implements the CLI playback seam by passing probe and device calls straight to
        // AudioDeviceFactory.
        dependency AudioDeviceFactoryPlaybackDeviceSource to ICliPlaybackDeviceSource;
        dependency AudioDeviceFactoryPlaybackDeviceSource to Speech_AudioDeviceFactory::AudioDeviceFactory;
        dependency AudioDeviceFactoryPlaybackDeviceSource to Speech_AudioDeviceSelection::AudioDeviceSelection;
        dependency AudioDeviceFactoryPlaybackDeviceSource to Speech_IAudioPlaybackDevice::IAudioPlaybackDevice;
        dependency AudioDeviceFactoryPlaybackDeviceSource to Speech_IAudioPlaybackDeviceProbe::IAudioPlaybackDeviceProbe;
    }
}
