using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="PlaybackAudioResampler"/>.
/// </summary>
public class PlaybackAudioResamplerTests
{
    /// <summary>
    ///     Proves that resampling at equal rates copies the input unchanged, so the common case
    ///     (engine rate already matches the device rate) costs no interpolation error.
    /// </summary>
    [Fact]
    public void PlaybackAudioResampler_Resample_EqualRates_CopiesUnchanged()
    {
        // Arrange
        float[] samples = [0.1f, 0.2f, 0.3f];

        // Act
        var result = PlaybackAudioResampler.Resample(samples, sourceSampleRate: 16000, targetSampleRate: 16000);

        // Assert
        Assert.Equal(samples, result);
    }

    /// <summary>
    ///     Proves that upsampling doubles the sample count via linear interpolation.
    /// </summary>
    [Fact]
    public void PlaybackAudioResampler_Resample_Upsample_ProducesInterpolatedSamples()
    {
        // Arrange: a two-sample ramp, upsampled to twice the rate
        float[] samples = [0.0f, 1.0f];

        // Act
        var result = PlaybackAudioResampler.Resample(samples, sourceSampleRate: 8000, targetSampleRate: 16000);

        // Assert
        Assert.Equal([0.0f, 0.5f, 1.0f, 1.0f], result, new FloatToleranceComparer());
    }

    /// <summary>
    ///     Proves that downsampling reduces the sample count proportionally and matches the
    ///     independently recomputed anti-aliased output.
    /// </summary>
    [Fact]
    public void PlaybackAudioResampler_Resample_Downsample_ReducesSampleCount()
    {
        // Arrange
        float[] samples = [0.0f, 0.25f, 0.5f, 0.75f];
        var expected = ResampleReference(samples, sourceSampleRate: 16000, targetSampleRate: 8000);

        // Act
        var result = PlaybackAudioResampler.Resample(samples, sourceSampleRate: 16000, targetSampleRate: 8000);

        // Assert
        Assert.Equal(expected, result, new FloatToleranceComparer());
    }

    /// <summary>
    ///     Proves that resampling an empty input produces an empty output.
    /// </summary>
    [Fact]
    public void PlaybackAudioResampler_Resample_EmptyInput_ReturnsEmpty()
    {
        // Act
        var result = PlaybackAudioResampler.Resample([], sourceSampleRate: 16000, targetSampleRate: 48000);

        // Assert
        Assert.Empty(result);
    }

    /// <summary>
    ///     Proves that resampling rejects a non-positive source or target rate.
    /// </summary>
    [Theory]
    [InlineData(0, 16000)]
    [InlineData(16000, 0)]
    [InlineData(-1, 16000)]
    public void PlaybackAudioResampler_Resample_NonPositiveRate_ThrowsArgumentOutOfRangeException(
        int sourceSampleRate,
        int targetSampleRate)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlaybackAudioResampler.Resample([0.1f], sourceSampleRate, targetSampleRate));
    }

    /// <summary>
    ///     Proves that a downsampled tone above the target Nyquist frequency is attenuated.
    /// </summary>
    [Fact]
    public void PlaybackAudioResampler_Resample_AboveTargetNyquistTone_IsAttenuated()
    {
        // Arrange
        var belowNyquist = GenerateSineWave(sourceSampleRate: 48000, frequencyHz: 1000, sampleCount: 480);
        var aboveNyquist = GenerateSineWave(sourceSampleRate: 48000, frequencyHz: 12000, sampleCount: 480);

        // Act
        var belowNyquistResampled = PlaybackAudioResampler.Resample(belowNyquist, 48000, 16000);
        var aboveNyquistResampled = PlaybackAudioResampler.Resample(aboveNyquist, 48000, 16000);

        // Assert
        Assert.True(
            ComputeRootMeanSquare(aboveNyquistResampled) < ComputeRootMeanSquare(belowNyquistResampled) * 0.35f);
    }

    /// <summary>
    ///     Proves that a short downsampling input does not throw even when shorter than the FIR
    ///     filter radius.
    /// </summary>
    [Fact]
    public void PlaybackAudioResampler_Resample_ShortInputDuringDownsampling_DoesNotThrow()
    {
        // Arrange
        float[] samples = [0.0f, 1.0f, 2.0f, 3.0f];

        // Act
        var exception = Record.Exception(() => PlaybackAudioResampler.Resample(samples, 32000, 16000));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that a single output channel copies mono samples unchanged.
    /// </summary>
    [Fact]
    public void PlaybackAudioResampler_UpmixToChannels_SingleChannel_CopiesUnchanged()
    {
        // Arrange
        float[] samples = [0.1f, 0.2f];

        // Act
        var result = PlaybackAudioResampler.UpmixToChannels(samples, channelCount: 1);

        // Assert
        Assert.Equal(samples, result);
    }

    /// <summary>
    ///     Proves that upmixing to multiple channels replicates each mono sample across every
    ///     channel, interleaved per frame.
    /// </summary>
    [Fact]
    public void PlaybackAudioResampler_UpmixToChannels_MultipleChannels_ReplicatesEachFrame()
    {
        // Arrange
        float[] samples = [0.1f, 0.2f];

        // Act
        var result = PlaybackAudioResampler.UpmixToChannels(samples, channelCount: 2);

        // Assert: each mono sample duplicated across both interleaved channels
        Assert.Equal([0.1f, 0.1f, 0.2f, 0.2f], result);
    }

    /// <summary>
    ///     Proves that upmixing an empty input produces an empty output regardless of channel count.
    /// </summary>
    [Fact]
    public void PlaybackAudioResampler_UpmixToChannels_EmptyInput_ReturnsEmpty()
    {
        // Act
        var result = PlaybackAudioResampler.UpmixToChannels([], channelCount: 2);

        // Assert
        Assert.Empty(result);
    }

    /// <summary>
    ///     Proves that upmixing rejects a non-positive channel count.
    /// </summary>
    [Fact]
    public void PlaybackAudioResampler_UpmixToChannels_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlaybackAudioResampler.UpmixToChannels([0.1f], channelCount: 0));
    }

    /// <summary>
    ///     Proves that <see cref="PlaybackAudioResampler.Convert"/> composes resampling and
    ///     upmixing: a mono source at a different rate becomes interleaved stereo at the target
    ///     rate.
    /// </summary>
    [Fact]
    public void PlaybackAudioResampler_Convert_DifferentRateAndChannels_ResamplesThenUpmixes()
    {
        // Arrange: mono 8 kHz source converted to stereo 16 kHz
        var resampler = new PlaybackAudioResampler(
            sourceSampleRate: 8000,
            targetSampleRate: 16000,
            targetChannelCount: 2);
        float[] samples = [0.5f, 1.0f];

        // Act
        var result = resampler.Convert(samples);

        // Assert: resampled to 4 mono samples, then interleaved to 8 stereo samples
        Assert.Equal(
            [0.5f, 0.5f, 0.75f, 0.75f, 1.0f, 1.0f, 1.0f, 1.0f],
            result,
            new FloatToleranceComparer());
    }

    /// <summary>
    ///     Proves that the constructor rejects a non-positive source rate, target rate, or
    ///     target channel count.
    /// </summary>
    [Theory]
    [InlineData(0, 16000, 1)]
    [InlineData(16000, 0, 1)]
    [InlineData(16000, 16000, 0)]
    public void PlaybackAudioResampler_Constructor_NonPositiveArgument_ThrowsArgumentOutOfRangeException(
        int sourceSampleRate,
        int targetSampleRate,
        int targetChannelCount)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PlaybackAudioResampler(sourceSampleRate, targetSampleRate, targetChannelCount));
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
    private static float[] ApplyReferenceLowpassFilter(ReadOnlySpan<float> monoSamples, IReadOnlyList<float> kernel)
    {
        var filtered = new float[monoSamples.Length];
        var radius = kernel.Count / 2;
        var lastIndex = monoSamples.Length - 1;
        for (var sampleIndex = 0; sampleIndex < monoSamples.Length; sampleIndex++)
        {
            var sum = 0.0;
            for (var tap = 0; tap < kernel.Count; tap++)
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
    ///     Compares float samples within a small tolerance.
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
