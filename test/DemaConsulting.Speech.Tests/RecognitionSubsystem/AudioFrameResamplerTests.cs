using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="AudioFrameResampler"/>, exercised as a pure function over plain
///     float arrays with no capture device, engine, or native runtime involved.
/// </summary>
public class AudioFrameResamplerTests
{
    /// <summary>
    ///     Proves that mono audio already at the model's rate passes through unchanged, so the
    ///     common "device already matches the model" case introduces no interpolation error.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_Convert_MonoAtTargetRate_ReturnsSamplesUnchanged()
    {
        // Arrange: a mono 16 kHz source and a 16 kHz model
        var resampler = new AudioFrameResampler(16000, 1, 16000);
        float[] samples = [0.1f, -0.2f, 0.3f, -0.4f];

        // Act: convert one block
        var converted = resampler.Convert(samples);

        // Assert: the block is byte-for-byte the same signal
        Assert.Equal(samples, converted);
    }

    /// <summary>
    ///     Proves that an empty capture block converts to an empty result rather than throwing,
    ///     since a device may legitimately deliver a zero-length block.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_Convert_EmptyInput_ReturnsEmptyResult()
    {
        // Arrange: a stereo 48 kHz source and a 16 kHz model
        var resampler = new AudioFrameResampler(48000, 2, 16000);

        // Act: convert an empty block
        var converted = resampler.Convert([]);

        // Assert: the result is empty, not null, and no exception was thrown
        Assert.Empty(converted);
    }

    /// <summary>
    ///     Proves that a stereo block is collapsed to mono by averaging each frame's channels.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_DownmixToMono_StereoInput_AveragesChannelsPerFrame()
    {
        // Arrange: two interleaved stereo frames, (0.0, 1.0) and (-1.0, 0.0)
        float[] interleaved = [0.0f, 1.0f, -1.0f, 0.0f];

        // Act: collapse to mono
        var mono = AudioFrameResampler.DownmixToMono(interleaved, 2);

        // Assert: each frame becomes the mean of its two channels
        Assert.Equal([0.5f, -0.5f], mono);
    }

    /// <summary>
    ///     Proves that a trailing partial frame is discarded, because a frame missing channels
    ///     cannot be averaged correctly.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_DownmixToMono_TrailingPartialFrame_DiscardsPartialFrame()
    {
        // Arrange: one complete stereo frame followed by a single orphaned channel sample
        float[] interleaved = [0.2f, 0.4f, 0.9f];

        // Act: collapse to mono
        var mono = AudioFrameResampler.DownmixToMono(interleaved, 2);

        // Assert: only the complete frame survives
        Assert.Equal([0.3f], mono, new FloatToleranceComparer());
    }

    /// <summary>
    ///     Proves that a single-channel block is passed through the downmix step unchanged.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_DownmixToMono_SingleChannel_ReturnsSamplesUnchanged()
    {
        // Arrange: an already-mono block
        float[] interleaved = [0.1f, 0.2f, 0.3f];

        // Act: collapse to mono
        var mono = AudioFrameResampler.DownmixToMono(interleaved, 1);

        // Assert: the samples are unchanged
        Assert.Equal(interleaved, mono);
    }

    /// <summary>
    ///     Proves that a zero or negative channel count is rejected, since no meaningful
    ///     interleaving stride exists for it.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_DownmixToMono_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: a valid block but an invalid stride
        float[] interleaved = [0.1f, 0.2f];

        // Act & Assert: the invalid stride is rejected
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AudioFrameResampler.DownmixToMono(interleaved, 0));
    }

    /// <summary>
    ///     Proves that halving the sample rate produces half as many samples, taken from the
    ///     matching positions in the source signal.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_Resample_Downsampling_ProducesProportionallyFewerSamples()
    {
        // Arrange: four mono samples being taken from 32 kHz down to 16 kHz
        float[] mono = [0.0f, 1.0f, 2.0f, 3.0f];

        // Act: resample at half rate
        var resampled = AudioFrameResampler.Resample(mono, 32000, 16000);

        // Assert: every second source sample is selected exactly
        Assert.Equal([0.0f, 2.0f], resampled, new FloatToleranceComparer());
    }

    /// <summary>
    ///     Proves that doubling the sample rate produces twice as many samples, with the new
    ///     samples linearly interpolated between their neighbors.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_Resample_Upsampling_LinearlyInterpolatesBetweenSamples()
    {
        // Arrange: two mono samples being taken from 8 kHz up to 16 kHz
        float[] mono = [0.0f, 1.0f];

        // Act: resample at double rate
        var resampled = AudioFrameResampler.Resample(mono, 8000, 16000);

        // Assert: twice as many samples, with the new sample interpolated midway and the
        // trailing positions clamped to the last input sample
        Assert.Equal([0.0f, 0.5f, 1.0f, 1.0f], resampled, new FloatToleranceComparer());
    }

    /// <summary>
    ///     Proves that a single input sample survives resampling rather than producing an empty
    ///     or out-of-range read.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_Resample_SingleSample_ClampsToThatSample()
    {
        // Arrange: one mono sample being taken from 8 kHz up to 24 kHz
        float[] mono = [0.75f];

        // Act: resample at triple rate
        var resampled = AudioFrameResampler.Resample(mono, 8000, 24000);

        // Assert: every output sample clamps to the only available input sample
        Assert.Equal([0.75f, 0.75f, 0.75f], resampled, new FloatToleranceComparer());
    }

    /// <summary>
    ///     Proves that a conversion rounding down to zero output samples returns an empty result
    ///     rather than throwing.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_Resample_OutputRoundsToZeroSamples_ReturnsEmptyResult()
    {
        // Arrange: a single 48 kHz sample being taken down to 16 kHz (1 * 16000 / 48000 == 0)
        float[] mono = [0.5f];

        // Act: resample down
        var resampled = AudioFrameResampler.Resample(mono, 48000, 16000);

        // Assert: the result is empty rather than a fabricated sample
        Assert.Empty(resampled);
    }

    /// <summary>
    ///     Proves that a full stereo-to-mono, 48 kHz-to-16 kHz conversion applies both stages in
    ///     order, which is the realistic desktop-microphone case.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_Convert_StereoAtHigherRate_DownmixesAndResamples()
    {
        // Arrange: six interleaved stereo frames at 48 kHz feeding a 16 kHz model
        var resampler = new AudioFrameResampler(48000, 2, 16000);
        float[] interleaved =
        [
            0.0f, 0.0f,
            1.0f, 1.0f,
            2.0f, 2.0f,
            3.0f, 3.0f,
            4.0f, 4.0f,
            5.0f, 5.0f
        ];

        // Act: convert one block
        var converted = resampler.Convert(interleaved);

        // Assert: six stereo frames become six mono samples, then every third is selected
        Assert.Equal([0.0f, 3.0f], converted, new FloatToleranceComparer());
    }

    /// <summary>
    ///     Proves that the resampler rejects a non-positive source rate at construction, since no
    ///     meaningful conversion exists for it.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_Constructor_NonPositiveSourceRate_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert: a zero source rate is rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioFrameResampler(0, 1, 16000));
    }

    /// <summary>
    ///     Proves that the resampler rejects a non-positive target rate at construction.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_Constructor_NonPositiveTargetRate_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert: a negative target rate is rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioFrameResampler(16000, 1, -1));
    }

    /// <summary>
    ///     Proves that the resampler rejects a non-positive channel count at construction.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_Constructor_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert: a zero channel count is rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioFrameResampler(16000, 0, 16000));
    }

    /// <summary>
    ///     Compares float samples within a small tolerance, so tests assert on signal content
    ///     rather than on exact binary floating-point representation.
    /// </summary>
    private sealed class FloatToleranceComparer : IEqualityComparer<float>
    {
        /// <summary>The absolute difference two samples may differ by and still be considered equal.</summary>
        private const float Tolerance = 1e-5f;

        /// <inheritdoc/>
        public bool Equals(float x, float y) => Math.Abs(x - y) <= Tolerance;

        /// <inheritdoc/>
        public int GetHashCode(float obj) => obj.GetHashCode();
    }
}
