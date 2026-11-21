package Speech_AudioFrameResampler {
    private import SoftwareItemMetadata::*;
    private import Speech_WindowedSincLowpassFilter::*;

    part def AudioFrameResampler {
        doc /* Pure, immutable converter that collapses interleaved multi-channel capture audio to mono by channel
             * averaging and converts its sample rate by linear interpolation, so audio captured at any device format
             * can be fed to a model at its declared rate. A small windowed-sinc lowpass filter is applied only on the
             * downsampling path before decimation to reduce aliasing. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/AudioFrameResampler.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/AudioFrameResamplerTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/audio-frame-resampler.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/audio-frame-resampler.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/audio-frame-resampler.yaml");
        }

        // Uses the shared windowed-sinc lowpass filter when downsampling capture audio.
        dependency AudioFrameResampler to WindowedSincLowpassFilter;
    }
}
