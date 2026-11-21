package Speech_PortAudioCaptureDeviceProbe {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioDeviceDescription::*;
    private import Speech_IAudioCaptureDeviceProbe::*;
    private import Speech_PortAudioEnvironment::*;

    part def PortAudioCaptureDeviceProbe {
        doc /* Preferred-host filtered capture-device probe backed by PortAudio device metadata exposed through the
             * internal PortAudio seam. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/PortAudioCaptureDeviceProbe.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudioCaptureDeviceProbeTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/port-audio-capture-device-probe.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/port-audio-capture-device-probe.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/port-audio-capture-device-probe.yaml");
        }

        // Implements the capture probe contract using device metadata from the shared PortAudio
        // environment.
        dependency PortAudioCaptureDeviceProbe to AudioDeviceDescription;
        dependency PortAudioCaptureDeviceProbe to IAudioCaptureDeviceProbe;
        dependency PortAudioCaptureDeviceProbe to PortAudioEnvironment;
    }
}
