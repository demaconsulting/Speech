package SpeechCli_DeviceCommandsSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_DevicesTestCommand::*;
    private import SpeechCli_DoctorCommand::*;
    private import SpeechCli_ListDevicesCommand::*;

    part def DeviceCommandsSubsystem {
        doc /* The three device-related subcommands (list-devices, devices test, doctor), depending directly on the
             * library's public AudioDeviceFactory/IAudioCaptureDeviceProbe/IAudioPlaybackDeviceProbe types with no
             * CLI-owned seam wrapper, since those interfaces are already public and directly fakeable. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-cli/device-commands-subsystem.md");
            verificationFiles = ("docs/verification/speech-cli/device-commands-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-cli/device-commands-subsystem.yaml");
        }

        part listDevicesCommand : ListDevicesCommand;
        part devicesTestCommand : DevicesTestCommand;
        part doctorCommand : DoctorCommand;
    }
}
