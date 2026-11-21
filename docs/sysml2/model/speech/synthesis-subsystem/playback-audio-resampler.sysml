package Speech_PlaybackAudioResampler {
    private import SoftwareItemMetadata::*;
    private import Speech_WindowedSincLowpassFilter::*;

    part def PlaybackAudioResampler {
        doc /* Pure, immutable converter that resamples single-channel synthesized audio from the synthesis engine's
             * sample rate to the playback device's sample rate by linear interpolation, applying a small
             * windowed-sinc lowpass filter only on the downsampling path before decimation, then upmixes it across
             * the playback device's required channel count, mirroring AudioFrameResampler's capture-direction
             * conversion. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/PlaybackAudioResampler.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/PlaybackAudioResamplerTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/playback-audio-resampler.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/playback-audio-resampler.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/playback-audio-resampler.yaml");
        }

        // Uses the shared windowed-sinc lowpass filter when downsampling synthesized audio.
        dependency PlaybackAudioResampler to WindowedSincLowpassFilter;
    }
}
