package Speech_PortAudioDeviceInfo {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    part def PortAudioDeviceInfo {
        doc /* Managed metadata record describing one PortAudio device without exposing native binding structs to
             * higher-level logic. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/PortAudio/PortAudioDeviceInfo.cs");
            designFiles = ("docs/design/speech/audio-subsystem/port-audio/port-audio-supporting-types.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/port-audio/port-audio-supporting-types.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/port-audio/port-audio-supporting-types.yaml");
        }

        attribute name : String;
        attribute hostApiIndex : Integer;
        attribute maxInputChannels : Integer;
        attribute maxOutputChannels : Integer;
        attribute defaultSampleRate : Integer;
        attribute defaultLowInputLatency : Real;
        attribute defaultLowOutputLatency : Real;
    }
}
