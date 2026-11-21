package Speech_PortAudioPlaybackDeviceProbe {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioDeviceDescription::*;
    private import Speech_IAudioPlaybackDeviceProbe::*;
    private import Speech_PortAudioEnvironment::*;

    part def PortAudioPlaybackDeviceProbe {
        doc /* Preferred-host filtered playback-device probe backed by PortAudio device metadata exposed through the
             * internal PortAudio seam. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/PortAudioPlaybackDeviceProbe.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudioPlaybackDeviceProbeTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/port-audio-playback-device-probe.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/port-audio-playback-device-probe.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/port-audio-playback-device-probe.yaml");
        }

        // Implements the playback probe contract using device metadata from the shared PortAudio
        // environment.
        dependency PortAudioPlaybackDeviceProbe to AudioDeviceDescription;
        dependency PortAudioPlaybackDeviceProbe to IAudioPlaybackDeviceProbe;
        dependency PortAudioPlaybackDeviceProbe to PortAudioEnvironment;
    }
}
