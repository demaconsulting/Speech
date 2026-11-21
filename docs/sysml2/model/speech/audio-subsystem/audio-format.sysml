package Speech_AudioFormat {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    part def AudioFormat {
        doc /* Immutable validating value that carries sample rate and channel count together, so model declarations
             * and audio-device composition can exchange audio-format intent without leaking any backend-native type
             * into the public API. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/AudioFormat.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/AudioFormatTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/audio-format.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/audio-format.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/audio-format.yaml");
        }

        attribute sampleRate : Integer;
        attribute channelCount : Integer;
    }
}
