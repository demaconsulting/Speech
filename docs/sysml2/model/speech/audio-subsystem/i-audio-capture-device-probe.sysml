package Speech_IAudioCaptureDeviceProbe {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioDeviceDescription::*;

    part def IAudioCaptureDeviceProbe {
        doc /* Contract for enumerating available audio capture devices, independent of any real hardware or native
             * runtime. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/IAudioCaptureDeviceProbe.cs");
            designFiles = ("docs/design/speech/audio-subsystem/i-audio-capture-device-probe.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/i-audio-capture-device-probe.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/i-audio-capture-device-probe.yaml");
        }

        // Reports the available capture devices as device descriptions.
        dependency IAudioCaptureDeviceProbe to AudioDeviceDescription;
    }
}
