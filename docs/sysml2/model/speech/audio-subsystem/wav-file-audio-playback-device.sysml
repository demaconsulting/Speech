package Speech_WavFileAudioPlaybackDevice {
    private import SoftwareItemMetadata::*;
    private import Speech_IAudioPlaybackDevice::*;

    part def WavFileAudioPlaybackDevice {
        doc /* File-backed IAudioPlaybackDevice implementation that writes queued samples to a WAV file as 16-bit PCM
             * instead of rendering them through real playback hardware, always available and clamping samples before
             * scaling to match the byte layout promoted from the demo's temporary WavFileWriter. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/WavFileAudioPlaybackDevice.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/WavFileAudioPlaybackDeviceTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/wav-file-audio-playback-device.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/wav-file-audio-playback-device.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/wav-file-audio-playback-device.yaml");
        }

        // Implements the playback contract by writing queued samples to a WAV file.
        dependency WavFileAudioPlaybackDevice to IAudioPlaybackDevice;
    }
}
