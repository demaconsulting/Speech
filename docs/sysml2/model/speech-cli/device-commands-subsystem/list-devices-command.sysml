package SpeechCli_ListDevicesCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_Context::*;

    part def ListDevicesCommand {
        doc /* Implements list-devices: enumerates capture/playback probes, optionally filtered by --direction, and
             * prints an aligned table or a "no devices found" message. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/DeviceCommandsSubsystem/ListDevicesCommand.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/DeviceCommandsSubsystem/ListDevicesCommandTests.cs");
            designFiles = ("docs/design/speech-cli/device-commands-subsystem/list-devices-command.md");
            verificationFiles = ("docs/verification/speech-cli/device-commands-subsystem/list-devices-command.md");
            requirementFiles = ("docs/reqstream/speech-cli/device-commands-subsystem/list-devices-command.yaml");
        }

        // Lists device descriptions from AudioDeviceFactory's capture and playback probes in a table.
        dependency ListDevicesCommand to Context;
        dependency ListDevicesCommand to Speech_AudioDeviceDescription::AudioDeviceDescription;
        dependency ListDevicesCommand to Speech_AudioDeviceFactory::AudioDeviceFactory;
        dependency ListDevicesCommand to Speech_IAudioCaptureDeviceProbe::IAudioCaptureDeviceProbe;
        dependency ListDevicesCommand to Speech_IAudioPlaybackDeviceProbe::IAudioPlaybackDeviceProbe;
    }
}
