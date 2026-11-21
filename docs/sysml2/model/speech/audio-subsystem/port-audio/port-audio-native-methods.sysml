package Speech_PortAudioNativeMethods {
    private import SoftwareItemMetadata::*;
    private import Speech_PortAudioHostApiType::*;

    part def PortAudioNativeMethods {
        doc /* Repository-local P/Invoke declarations for PortAudio host-API functions absent from PortAudioSharp2. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/PortAudio/PortAudioNativeMethods.cs");
            designFiles = ("docs/design/speech/audio-subsystem/port-audio/port-audio-supporting-types.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/port-audio/port-audio-supporting-types.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/port-audio/port-audio-supporting-types.yaml");
        }

        // Declares native host-API functions in terms of the host-API type identifiers.
        dependency PortAudioNativeMethods to PortAudioHostApiType;
    }
}
