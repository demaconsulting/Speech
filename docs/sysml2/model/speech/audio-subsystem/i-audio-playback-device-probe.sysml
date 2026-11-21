package Speech_IAudioPlaybackDeviceProbe {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioDeviceDescription::*;

    part def IAudioPlaybackDeviceProbe {
        doc /* Contract for enumerating available audio playback devices, independent of any real hardware or native
             * runtime. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/IAudioPlaybackDeviceProbe.cs");
            designFiles = ("docs/design/speech/audio-subsystem/i-audio-playback-device-probe.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/i-audio-playback-device-probe.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/i-audio-playback-device-probe.yaml");
        }

        // Reports the available playback devices as device descriptions.
        dependency IAudioPlaybackDeviceProbe to AudioDeviceDescription;
    }
}
