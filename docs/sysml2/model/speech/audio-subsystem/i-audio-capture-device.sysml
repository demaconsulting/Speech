package Speech_IAudioCaptureDevice {
    private import SoftwareItemMetadata::*;

    part def IAudioCaptureDevice {
        doc /* Contract for an audio capture device (availability flag, start/stop, captured-frame event), independent
             * of any real hardware or native runtime. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/IAudioCaptureDevice.cs");
            designFiles = ("docs/design/speech/audio-subsystem/i-audio-capture-device.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/i-audio-capture-device.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/i-audio-capture-device.yaml");
        }
    }
}
