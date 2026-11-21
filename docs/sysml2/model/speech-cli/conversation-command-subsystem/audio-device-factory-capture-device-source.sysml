package SpeechCli_AudioDeviceFactoryCaptureDeviceSource {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_ICliCaptureDeviceSource::*;

    part def AudioDeviceFactoryCaptureDeviceSource {
        doc /* Production ICliCaptureDeviceSource implementation, forwarding every call unchanged to a real, composed
             * AudioDeviceFactory - including its hardware-detection behavior - so real capture-device resolution is
             * exactly the factory's own. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/ConversationCommandSubsystem/AudioDeviceFactoryCaptureDeviceSource.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/ConversationCommandSubsystem/AudioDeviceFactoryCaptureDeviceSourceTests.cs");
            designFiles = ("docs/design/speech-cli/conversation-command-subsystem/audio-device-factory-capture-device-source.md");
            verificationFiles = ("docs/verification/speech-cli/conversation-command-subsystem/audio-device-factory-capture-device-source.md");
            requirementFiles = ("docs/reqstream/speech-cli/conversation-command-subsystem/audio-device-factory-capture-device-source.yaml");
        }

        // Implements the CLI capture seam by passing probe and device calls straight to
        // AudioDeviceFactory.
        dependency AudioDeviceFactoryCaptureDeviceSource to ICliCaptureDeviceSource;
        dependency AudioDeviceFactoryCaptureDeviceSource to Speech_AudioDeviceFactory::AudioDeviceFactory;
        dependency AudioDeviceFactoryCaptureDeviceSource to Speech_AudioDeviceSelection::AudioDeviceSelection;
        dependency AudioDeviceFactoryCaptureDeviceSource to Speech_IAudioCaptureDevice::IAudioCaptureDevice;
        dependency AudioDeviceFactoryCaptureDeviceSource to Speech_IAudioCaptureDeviceProbe::IAudioCaptureDeviceProbe;
    }
}
