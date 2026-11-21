package Speech_PortAudioHostApiType {
    private import SoftwareItemMetadata::*;

    enum def PortAudioHostApiTypeKind {
        enum CoreAudio;
        enum Alsa;
        enum Wasapi;
    }

    part def PortAudioHostApiType {
        doc /* Internal enum carrying the stable PortAudio host-API constants used by platform policy before runtime
             * host-API indices are resolved. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/PortAudio/PortAudioHostApiType.cs");
            designFiles = ("docs/design/speech/audio-subsystem/port-audio/port-audio-supporting-types.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/port-audio/port-audio-supporting-types.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/port-audio/port-audio-supporting-types.yaml");
        }
    }
}
