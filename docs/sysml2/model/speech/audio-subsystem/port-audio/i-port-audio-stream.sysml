package Speech_IPortAudioStream {
    private import SoftwareItemMetadata::*;

    part def IPortAudioStream {
        doc /* Internal seam for a started or startable PortAudio callback stream, allowing unit tests to simulate
             * capture and playback without loading a native runtime. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/PortAudio/IPortAudioStream.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudioCaptureDeviceTests.cs", "test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudioPlaybackDeviceTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/port-audio/i-port-audio-stream.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/port-audio/i-port-audio-stream.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/port-audio/i-port-audio-stream.yaml");
        }
    }
}
