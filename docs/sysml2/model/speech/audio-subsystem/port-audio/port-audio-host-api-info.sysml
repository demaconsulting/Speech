package Speech_PortAudioHostApiInfo {
    private import SoftwareItemMetadata::*;
    private import Speech_PortAudioHostApiType::*;
    private import ScalarValues::*;

    part def PortAudioHostApiInfo {
        doc /* Managed metadata record describing one PortAudio host API, including its stable type identifier and
             * default input/output device indices. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/PortAudio/PortAudioHostApiInfo.cs");
            designFiles = ("docs/design/speech/audio-subsystem/port-audio/port-audio-supporting-types.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/port-audio/port-audio-supporting-types.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/port-audio/port-audio-supporting-types.yaml");
        }

        attribute name : String;
        attribute defaultInputDeviceIndex : Integer;
        attribute defaultOutputDeviceIndex : Integer;
        attribute deviceCount : Integer;

        // Records each host API's stable type identifier.
        dependency PortAudioHostApiInfo to PortAudioHostApiType;
    }
}
