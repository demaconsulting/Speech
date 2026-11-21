package Speech_AudioResampler {
    private import SoftwareItemMetadata::*;

    part def AudioResampler {
        doc /* Down-mixes and windowed-sinc resamples floating-point audio. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/AudioResampler.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/AudioResamplerTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/audio-resampler.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/audio-resampler.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/audio-resampler.yaml");
        }

        // Produces down-mixed, resampled audio as new AudioClip values.
        dependency AudioResampler to Speech_AudioClip::AudioClip;
    }
}
