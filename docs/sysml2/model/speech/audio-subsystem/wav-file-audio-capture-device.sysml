package Speech_WavFileAudioCaptureDevice {
    private import SoftwareItemMetadata::*;
    private import Speech_IAudioCaptureDevice::*;

    part def WavFileAudioCaptureDevice {
        doc /* File-backed IAudioCaptureDevice implementation that synchronously reads a mono, 16-bit PCM WAV file and
             * delivers it through FrameCaptured, always available and exposing one additive EndOfFileReached event
             * raised once the file is fully consumed, since the interface itself has no end-of-stream concept. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/WavFileAudioCaptureDevice.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/WavFileAudioCaptureDeviceTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/wav-file-audio-capture-device.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/wav-file-audio-capture-device.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/wav-file-audio-capture-device.yaml");
        }

        // Implements the capture contract by reading WAV samples, using tensor primitives for conversion.
        dependency WavFileAudioCaptureDevice to IAudioCaptureDevice;
        dependency WavFileAudioCaptureDevice to OtsDependencies::SystemNumericsTensors;
    }
}
