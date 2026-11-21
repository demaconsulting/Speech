package Speech_AudioDeviceFactory {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioDeviceDescription::*;
    private import Speech_AudioDeviceSelection::*;
    private import Speech_AudioFormat::*;
    private import Speech_IAudioCaptureDevice::*;
    private import Speech_IAudioCaptureDeviceProbe::*;
    private import Speech_IAudioPlaybackDevice::*;
    private import Speech_IAudioPlaybackDeviceProbe::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_NullSpeechDiagnostics::*;
    private import Speech_PortAudioCaptureDevice::*;
    private import Speech_PortAudioCaptureDeviceProbe::*;
    private import Speech_PortAudioEnvironment::*;
    private import Speech_PortAudioPlaybackDevice::*;
    private import Speech_PortAudioPlaybackDeviceProbe::*;
    private import Speech_SpeechDiagnosticLevel::*;
    private import Speech_UnavailableAudioCaptureDevice::*;
    private import Speech_UnavailableAudioCaptureDeviceProbe::*;
    private import Speech_UnavailableAudioPlaybackDevice::*;
    private import Speech_UnavailableAudioPlaybackDeviceProbe::*;

    part def AudioDeviceFactory {
        doc /* Composition entry point for obtaining audio capture/playback devices and probes; exposes real
             * PortAudio-backed defaults when the runtime initializes and otherwise degrades safely to the
             * Unavailable* fallbacks without throwing at composition. The factory also accepts an optional preferred
             * AudioFormat hint for capture/playback device construction, defaulting to device-native behavior when
             * omitted. RefreshDevices() forces the underlying PortAudio device table to be re-scanned on demand
             * (hot-plug support), refusing with AudioDeviceInUseException while any created device has an active
             * stream; this method is not safe to call concurrently with itself or with concurrent
             * CaptureProbe/PlaybackProbe reads without the caller's own external synchronization. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/AudioDeviceFactory.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/AudioDeviceFactoryTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/audio-device-factory.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/audio-device-factory.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/audio-device-factory.yaml");
        }

        // Builds PortAudio-backed devices and probes, falling back to Unavailable stand-ins and reporting
        // via diagnostics.
        dependency AudioDeviceFactory to AudioDeviceDescription;
        dependency AudioDeviceFactory to AudioDeviceSelection;
        dependency AudioDeviceFactory to AudioFormat;
        dependency AudioDeviceFactory to IAudioCaptureDevice;
        dependency AudioDeviceFactory to IAudioCaptureDeviceProbe;
        dependency AudioDeviceFactory to IAudioPlaybackDevice;
        dependency AudioDeviceFactory to IAudioPlaybackDeviceProbe;
        dependency AudioDeviceFactory to ISpeechDiagnostics;
        dependency AudioDeviceFactory to NullSpeechDiagnostics;
        dependency AudioDeviceFactory to PortAudioCaptureDevice;
        dependency AudioDeviceFactory to PortAudioCaptureDeviceProbe;
        dependency AudioDeviceFactory to PortAudioEnvironment;
        dependency AudioDeviceFactory to PortAudioPlaybackDevice;
        dependency AudioDeviceFactory to PortAudioPlaybackDeviceProbe;
        dependency AudioDeviceFactory to SpeechDiagnosticLevel;
        dependency AudioDeviceFactory to UnavailableAudioCaptureDevice;
        dependency AudioDeviceFactory to UnavailableAudioCaptureDeviceProbe;
        dependency AudioDeviceFactory to UnavailableAudioPlaybackDevice;
        dependency AudioDeviceFactory to UnavailableAudioPlaybackDeviceProbe;
    }
}
