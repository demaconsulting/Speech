package SpeechCli_DevicesTestCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_Context::*;

    part def DevicesTestCommand {
        doc /* Implements devices test: plays an in-CLI synthesized test tone (output, the default) or records a short
             * clip and reports basic statistics (input), validating any requested --device name before device
             * creation. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/DeviceCommandsSubsystem/DevicesTestCommand.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/DeviceCommandsSubsystem/DevicesTestCommandTests.cs");
            designFiles = ("docs/design/speech-cli/device-commands-subsystem/devices-test-command.md");
            verificationFiles = ("docs/verification/speech-cli/device-commands-subsystem/devices-test-command.md");
            requirementFiles = ("docs/reqstream/speech-cli/device-commands-subsystem/devices-test-command.yaml");
        }

        // Opens devices from AudioDeviceFactory to play a tensor-generated tone or record and analyze a
        // clip.
        dependency DevicesTestCommand to Context;
        dependency DevicesTestCommand to Speech_AudioDeviceDescription::AudioDeviceDescription;
        dependency DevicesTestCommand to Speech_AudioDeviceFactory::AudioDeviceFactory;
        dependency DevicesTestCommand to Speech_AudioDeviceSelection::AudioDeviceSelection;
        dependency DevicesTestCommand to Speech_IAudioCaptureDevice::IAudioCaptureDevice;
        dependency DevicesTestCommand to Speech_IAudioPlaybackDevice::IAudioPlaybackDevice;
        dependency DevicesTestCommand to OtsDependencies::SystemNumericsTensors;
    }
}
