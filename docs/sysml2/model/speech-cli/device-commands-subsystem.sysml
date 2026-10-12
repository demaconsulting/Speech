package SpeechCli {
    part def DeviceCommandsSubsystem {
        doc /* The three device-related subcommands (list-devices, devices test, doctor),
             * depending directly on the library's public AudioDeviceFactory/
             * IAudioCaptureDeviceProbe/IAudioPlaybackDeviceProbe types with no CLI-owned seam
             * wrapper, since those interfaces are already public and directly fakeable. */

        comment designRef /* Design: docs/design/speech-cli/device-commands-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/device-commands-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/device-commands-subsystem.yaml */

        part listDevicesCommand : ListDevicesCommand;
        part devicesTestCommand : DevicesTestCommand;
        part doctorCommand : DoctorCommand;
    }
}
