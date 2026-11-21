package Speech_PortAudioApi {
    private import SoftwareItemMetadata::*;
    private import Speech_IPortAudioApi::*;
    private import Speech_IPortAudioStream::*;
    private import Speech_PortAudioDeviceInfo::*;
    private import Speech_PortAudioHostApiInfo::*;
    private import Speech_PortAudioHostApiType::*;
    private import Speech_PortAudioNativeMethods::*;

    part def PortAudioApi {
        doc /* Concrete production adapter that combines PortAudioSharp2-managed calls with repository-local native
             * host-API bindings behind the IPortAudioApi seam. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/PortAudio/PortAudioApi.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudio/PortAudioEnvironmentTests.cs", "test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudio/PortAudioApiTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/port-audio/port-audio-api.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/port-audio/port-audio-api.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/port-audio/port-audio-api.yaml");
        }

        // Implements the PortAudio seam with PortAudioSharp2 calls plus local P/Invokes for host-API
        // functions.
        dependency PortAudioApi to IPortAudioApi;
        dependency PortAudioApi to IPortAudioStream;
        dependency PortAudioApi to PortAudioDeviceInfo;
        dependency PortAudioApi to PortAudioHostApiInfo;
        dependency PortAudioApi to PortAudioHostApiType;
        dependency PortAudioApi to PortAudioNativeMethods;
        dependency PortAudioApi to OtsDependencies::PortAudioSharp2;
    }
}
