package Speech_UnavailableAudioCaptureDevice {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioDeviceUnavailableException::*;
    private import Speech_IAudioCaptureDevice::*;

    part def UnavailableAudioCaptureDevice {
        doc /* Honest "unavailable" IAudioCaptureDevice fallback: reports IsAvailable as false and throws
             * AudioDeviceUnavailableException only when an operational member is invoked. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/UnavailableAudioCaptureDevice.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/UnavailableAudioDeviceTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/unavailable-audio-capture-device.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/unavailable-audio-capture-device.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/unavailable-audio-capture-device.yaml");
        }

        // Implements the capture device contract and throws the unavailable exception on use.
        dependency UnavailableAudioCaptureDevice to AudioDeviceUnavailableException;
        dependency UnavailableAudioCaptureDevice to IAudioCaptureDevice;
    }
}
