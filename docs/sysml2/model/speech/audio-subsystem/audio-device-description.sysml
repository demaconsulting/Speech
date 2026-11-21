package Speech_AudioDeviceDescription {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    enum def AudioDeviceDirection {
        enum Capture;
        enum Playback;
    }

    part def AudioDeviceDescription {
        doc /* Immutable record describing one audio device by name (its sole identity key), direction, channel count,
             * and sample rate. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/AudioDeviceDescription.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/AudioDeviceDescriptionTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/audio-device-description.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/audio-device-description.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/audio-device-description.yaml");
        }

        attribute name : String;
        attribute direction : AudioDeviceDirection;
        attribute channelCount : Integer;
        attribute sampleRate : Integer;
    }
}
