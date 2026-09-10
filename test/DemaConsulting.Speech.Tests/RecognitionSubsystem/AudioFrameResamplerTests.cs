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
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioFrameResampler.DownmixToMono(interleaved, 0));
    }

    /// <summary>
    ///     Proves that halving the sample rate still produces half as many samples, but now from
    ///     the anti-aliased signal rather than from raw point-picking.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_Resample_Downsampling_ProducesProportionallyFewerSamples()
    {
        // Arrange: four mono samples being taken from 32 kHz down to 16 kHz
        float[] mono = [0.0f, 1.0f, 2.0f, 3.0f];
        var expected = ResampleReference(mono, 32000, 16000);

        // Act: resample at half rate
        var resampled = AudioFrameResampler.Resample(mono, 32000, 16000);

        // Assert: the production output matches the independently recomputed filtered signal
        Assert.Equal(expected, resampled, new FloatToleranceComparer());
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
    ///     order, now including the anti-aliasing filter before decimation.
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
        var expected = ResampleReference([0.0f, 1.0f, 2.0f, 3.0f, 4.0f, 5.0f], 48000, 16000);

        // Act: convert one block
        var converted = resampler.Convert(interleaved);

        // Assert: the downmixed mono signal is then anti-aliased and resampled
        Assert.Equal(expected, converted, new FloatToleranceComparer());
    }

    /// <summary>
    ///     Proves that downsampling attenuates a tone above the target Nyquist frequency.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_Resample_AboveTargetNyquistTone_IsAttenuated()
    {
        // Arrange: equally loud below-Nyquist and above-Nyquist tones at the source rate
        var belowNyquist = GenerateSineWave(sourceSampleRate: 48000, frequencyHz: 1000, sampleCount: 480);
        var aboveNyquist = GenerateSineWave(sourceSampleRate: 48000, frequencyHz: 12000, sampleCount: 480);

        // Act: downsample both tones to 16 kHz
        var belowNyquistResampled = AudioFrameResampler.Resample(belowNyquist, 48000, 16000);
        var aboveNyquistResampled = AudioFrameResampler.Resample(aboveNyquist, 48000, 16000);

        // Assert: the out-of-band tone is materially attenuated before aliasing can fold down
        Assert.True(
            ComputeRootMeanSquare(aboveNyquistResampled) < ComputeRootMeanSquare(belowNyquistResampled) * 0.35f);
    }

    /// <summary>
    ///     Proves that a downsampling input shorter than the filter radius still converts without
    ///     throwing.
    /// </summary>
    [Fact]
    public void AudioFrameResampler_Resample_ShortInputDuringDownsampling_DoesNotThrow()
    {
        // Arrange: a short signal well below the FIR kernel radius
        float[] mono = [0.0f, 1.0f, 2.0f, 3.0f];

        // Act
        var exception = Record.Exception(() => AudioFrameResampler.Resample(mono, 32000, 16000));

        // Assert
        Assert.Null(exception);
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
    ///     Computes the expected resampled signal independently for test assertions.
    /// </summary>
    /// <param name="monoSamples">The input mono samples.</param>
    /// <param name="sourceSampleRate">The input sample rate.</param>
    /// <param name="targetSampleRate">The output sample rate.</param>
    /// <returns>The independently recomputed expected output.</returns>
    private static float[] ResampleReference(
        ReadOnlySpan<float> monoSamples,
        int sourceSampleRate,
        int targetSampleRate)
    {
        if (monoSamples.IsEmpty || sourceSampleRate == targetSampleRate)
        {
            return monoSamples.ToArray();
        }

        var outputLength = (int)(monoSamples.Length * (long)targetSampleRate / sourceSampleRate);
        if (outputLength == 0)
        {
            return [];
        }

        var filtered = monoSamples.ToArray();
        if (targetSampleRate < sourceSampleRate)
        {
            filtered = ApplyReferenceLowpassFilter(
                filtered,
                BuildReferenceLowpassKernel((double)targetSampleRate / sourceSampleRate, 33));
        }

        var resampled = new float[outputLength];
        var step = (double)sourceSampleRate / targetSampleRate;
        var lastIndex = filtered.Length - 1;
        for (var index = 0; index < outputLength; index++)
        {
            var position = index * step;
            var lowerIndex = (int)position;
            if (lowerIndex >= lastIndex)
            {
                resampled[index] = filtered[lastIndex];
                continue;
            }

            var fraction = position - lowerIndex;
            var lower = filtered[lowerIndex];
            var upper = filtered[lowerIndex + 1];
            resampled[index] = (float)(lower + ((upper - lower) * fraction));
        }

        return resampled;
    }

    /// <summary>
    ///     Builds the reference Hamming-windowed sinc lowpass kernel used by the expected-value
    ///     helper.
    /// </summary>
    /// <param name="cutoffRatio">The cutoff ratio relative to the source Nyquist frequency.</param>
    /// <param name="tapCount">The odd-numbered FIR tap count.</param>
    /// <returns>The normalized kernel.</returns>
    private static float[] BuildReferenceLowpassKernel(double cutoffRatio, int tapCount)
    {
        var radius = tapCount / 2;
        var kernel = new float[tapCount];
        var sum = 0.0;
        for (var tap = 0; tap < tapCount; tap++)
        {
            var offset = tap - radius;
            var window = 0.54d - (0.46d * Math.Cos((2.0d * Math.PI * tap) / (tapCount - 1)));
            var radians = Math.PI * cutoffRatio * offset;
            var sinc = Math.Abs(radians) < double.Epsilon
                ? 1.0d
                : Math.Sin(radians) / radians;
            var coefficient = cutoffRatio * sinc * window;
            kernel[tap] = (float)coefficient;
            sum += coefficient;
        }

        for (var tap = 0; tap < kernel.Length; tap++)
        {
            kernel[tap] = (float)(kernel[tap] / sum);
        }

        return kernel;
    }

    /// <summary>
    ///     Applies the reference FIR kernel with replicated edge extension.
    /// </summary>
    /// <param name="monoSamples">The mono samples to filter.</param>
    /// <param name="kernel">The kernel to apply.</param>
    /// <returns>The filtered signal.</returns>
    private static float[] ApplyReferenceLowpassFilter(ReadOnlySpan<float> monoSamples, float[] kernel)
    {
        var filtered = new float[monoSamples.Length];
        var radius = kernel.Length / 2;
        var lastIndex = monoSamples.Length - 1;
        for (var sampleIndex = 0; sampleIndex < monoSamples.Length; sampleIndex++)
        {
            var sum = 0.0;
            for (var tap = 0; tap < kernel.Length; tap++)
            {
                var sourceIndex = Math.Clamp(sampleIndex + tap - radius, 0, lastIndex);
                sum += monoSamples[sourceIndex] * kernel[tap];
            }

            filtered[sampleIndex] = (float)sum;
        }

        return filtered;
    }

    /// <summary>
    ///     Generates one mono sine wave for attenuation comparisons.
    /// </summary>
    /// <param name="sourceSampleRate">The source sample rate.</param>
    /// <param name="frequencyHz">The sine-wave frequency.</param>
    /// <param name="sampleCount">The number of samples to generate.</param>
    /// <returns>The generated samples.</returns>
    private static float[] GenerateSineWave(int sourceSampleRate, int frequencyHz, int sampleCount)
    {
        var samples = new float[sampleCount];
        for (var index = 0; index < sampleCount; index++)
        {
            samples[index] = (float)Math.Sin((2.0d * Math.PI * frequencyHz * index) / sourceSampleRate);
        }

        return samples;
    }

    /// <summary>
    ///     Computes the root-mean-square amplitude of one signal.
    /// </summary>
    /// <param name="samples">The samples to measure.</param>
    /// <returns>The RMS amplitude.</returns>
    private static float ComputeRootMeanSquare(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
        {
            return 0.0f;
        }

        var sum = 0.0d;
        for (var index = 0; index < samples.Length; index++)
        {
            sum += samples[index] * samples[index];
        }

        return (float)Math.Sqrt(sum / samples.Length);
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
