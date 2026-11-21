package Speech_PortAudioCaptureDevice {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioDeviceSelection::*;
    private import Speech_AudioDeviceUnavailableException::*;
    private import Speech_AudioFormat::*;
    private import Speech_IAudioCaptureDevice::*;
    private import Speech_IPortAudioStream::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_NullSpeechDiagnostics::*;
    private import Speech_PortAudioDeviceInfo::*;
    private import Speech_PortAudioEnvironment::*;
    private import Speech_SpeechDiagnosticLevel::*;

    part def PortAudioCaptureDevice {
        doc /* Real PortAudio-backed capture device that resolves a selected or default input device, exposes honest
             * availability, raises captured frames through the public contract, and requests either the device
             * default format or a caller-supplied preferred AudioFormat whose channel count is clamped to device
             * capability. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/PortAudioCaptureDevice.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudioCaptureDeviceTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/port-audio-capture-device.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/port-audio-capture-device.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/port-audio-capture-device.yaml");
        }

        // Opens PortAudio input streams via the environment for the chosen device and reports failures to
        // diagnostics.
        dependency PortAudioCaptureDevice to AudioDeviceSelection;
        dependency PortAudioCaptureDevice to AudioDeviceUnavailableException;
        dependency PortAudioCaptureDevice to AudioFormat;
        dependency PortAudioCaptureDevice to IAudioCaptureDevice;
        dependency PortAudioCaptureDevice to IPortAudioStream;
        dependency PortAudioCaptureDevice to ISpeechDiagnostics;
        dependency PortAudioCaptureDevice to NullSpeechDiagnostics;
        dependency PortAudioCaptureDevice to PortAudioDeviceInfo;
        dependency PortAudioCaptureDevice to PortAudioEnvironment;
        dependency PortAudioCaptureDevice to SpeechDiagnosticLevel;
    }
}
