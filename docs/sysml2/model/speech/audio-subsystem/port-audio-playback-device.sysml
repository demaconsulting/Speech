package Speech_PortAudioPlaybackDevice {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioDeviceSelection::*;
    private import Speech_AudioDeviceUnavailableException::*;
    private import Speech_AudioFormat::*;
    private import Speech_IAudioPlaybackDevice::*;
    private import Speech_IPortAudioStream::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_NullSpeechDiagnostics::*;
    private import Speech_PortAudioDeviceInfo::*;
    private import Speech_PortAudioEnvironment::*;
    private import Speech_SpeechDiagnosticLevel::*;

    part def PortAudioPlaybackDevice {
        doc /* Real PortAudio-backed playback device that resolves a selected or default output device, exposes honest
             * availability, and drains queued samples into a callback stream with zero-fill on underrun while
             * requesting either the device default format or a caller-supplied preferred AudioFormat whose channel
             * count is clamped to device capability. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/PortAudioPlaybackDevice.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudioPlaybackDeviceTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/port-audio-playback-device.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/port-audio-playback-device.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/port-audio-playback-device.yaml");
        }

        // Opens PortAudio output streams via the environment for the chosen device and reports failures to
        // diagnostics.
        dependency PortAudioPlaybackDevice to AudioDeviceSelection;
        dependency PortAudioPlaybackDevice to AudioDeviceUnavailableException;
        dependency PortAudioPlaybackDevice to AudioFormat;
        dependency PortAudioPlaybackDevice to IAudioPlaybackDevice;
        dependency PortAudioPlaybackDevice to IPortAudioStream;
        dependency PortAudioPlaybackDevice to ISpeechDiagnostics;
        dependency PortAudioPlaybackDevice to NullSpeechDiagnostics;
        dependency PortAudioPlaybackDevice to PortAudioDeviceInfo;
        dependency PortAudioPlaybackDevice to PortAudioEnvironment;
        dependency PortAudioPlaybackDevice to SpeechDiagnosticLevel;
    }
}
