package Speech_AudioClip {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioResampler::*;
    private import ScalarValues::*;

    part def AudioClip {
        doc /* Immutable decoded-audio value with derived frame count, duration, mono and PCM16 views. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/AudioClip.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/AudioFileReaderTests.cs", "test/DemaConsulting.Speech.Tests/AudioSubsystem/AudioClipTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/audio-clip.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/audio-clip.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/audio-clip.yaml");
        }

        attribute samples : Real [0..*];
        attribute sampleRate : Integer;
        attribute channelCount : Integer;
        attribute bitsPerSample : Integer;

        // Delegates its derived mono and resampled views to the audio resampler.
        dependency AudioClip to AudioResampler;
    }
}
