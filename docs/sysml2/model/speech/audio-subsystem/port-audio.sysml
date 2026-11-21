package Speech_PortAudio {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;
    private import Speech_IPortAudioApi::*;
    private import Speech_IPortAudioStream::*;
    private import Speech_PortAudioApi::*;
    private import Speech_PortAudioDeviceInfo::*;
    private import Speech_PortAudioEnvironment::*;
    private import Speech_PortAudioHostApiInfo::*;
    private import Speech_PortAudioHostApiType::*;
    private import Speech_PortAudioNativeMethods::*;

    part def PortAudio {
        doc /* Internal child subsystem that isolates PortAudioSharp2 and supplementary native host-API interop behind
             * mockable managed abstractions for deterministic testing. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech/audio-subsystem/port-audio.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/port-audio.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/port-audio.yaml");
        }

        part iPortAudioApi : IPortAudioApi;
        part iPortAudioStream : IPortAudioStream;
        part portAudioEnvironment : PortAudioEnvironment;
        part portAudioApi : PortAudioApi;
        part portAudioDeviceInfo : PortAudioDeviceInfo;
        part portAudioHostApiInfo : PortAudioHostApiInfo;
        part portAudioHostApiType : PortAudioHostApiType;
        part portAudioNativeMethods : PortAudioNativeMethods;
        part portAudioSharp2 : PortAudioSharp2;
    }
}
