package Speech_UnavailableAudioPlaybackDevice {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioDeviceUnavailableException::*;
    private import Speech_IAudioPlaybackDevice::*;

    part def UnavailableAudioPlaybackDevice {
        doc /* Honest "unavailable" IAudioPlaybackDevice fallback: reports IsAvailable as false and throws
             * AudioDeviceUnavailableException only when an operational member is invoked. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/UnavailableAudioPlaybackDevice.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/UnavailableAudioDeviceTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/unavailable-audio-playback-device.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/unavailable-audio-playback-device.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/unavailable-audio-playback-device.yaml");
        }

        // Implements the playback device contract and throws the unavailable exception on use.
        dependency UnavailableAudioPlaybackDevice to AudioDeviceUnavailableException;
        dependency UnavailableAudioPlaybackDevice to IAudioPlaybackDevice;
    }
}
