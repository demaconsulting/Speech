package Speech_IAudioPlaybackDevice {
    private import SoftwareItemMetadata::*;

    part def IAudioPlaybackDevice {
        doc /* Contract for an audio playback device (availability flag, start/stop/write, reported channel
             * count/sample rate), independent of any real hardware or native runtime. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/IAudioPlaybackDevice.cs");
            designFiles = ("docs/design/speech/audio-subsystem/i-audio-playback-device.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/i-audio-playback-device.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/i-audio-playback-device.yaml");
        }
    }
}
