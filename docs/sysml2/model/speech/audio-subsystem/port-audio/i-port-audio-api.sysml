package Speech_IPortAudioApi {
    private import SoftwareItemMetadata::*;
    private import Speech_IPortAudioStream::*;
    private import Speech_PortAudioDeviceInfo::*;
    private import Speech_PortAudioHostApiInfo::*;
    private import Speech_PortAudioHostApiType::*;

    part def IPortAudioApi {
        doc /* Internal seam for PortAudio initialization and termination, host/device metadata access, and opening
             * capture or playback streams without exposing PortAudioSharp2 types. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/PortAudio/IPortAudioApi.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudio/PortAudioEnvironmentTests.cs", "test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudioCaptureDeviceTests.cs", "test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudioPlaybackDeviceTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/port-audio/i-port-audio-api.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/port-audio/i-port-audio-api.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/port-audio/i-port-audio-api.yaml");
        }

        // Exposes PortAudio streams, device info and host-API metadata as mockable types.
        dependency IPortAudioApi to IPortAudioStream;
        dependency IPortAudioApi to PortAudioDeviceInfo;
        dependency IPortAudioApi to PortAudioHostApiInfo;
        dependency IPortAudioApi to PortAudioHostApiType;
    }
}
