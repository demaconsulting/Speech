using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="WindowedSincLowpassFilter"/>, exercising the filter directly.
/// </summary>
public sealed class WindowedSincLowpassFilterTests
{
    /// <summary>The source sample rate used by the tone tests, in hertz.</summary>
    private const int SourceRate = 48000;

    /// <summary>Proves a tone above the cutoff is attenuated by at least 30 dB.</summary>
    [Fact]
    public void WindowedSincLowpassFilter_ApplyLowpassFilter_ToneAboveCutoff_IsAttenuated()
    {
        // Arrange: a 16 kHz target has an 8 kHz Nyquist, one third of the 24 kHz source Nyquist
        var kernel = WindowedSincLowpassFilter.BuildLowpassKernel(1.0 / 3.0, WindowedSincLowpassFilter.DownsamplingFilterTapCount);
        var tone = Tone(12000);

        // Act
        var filtered = WindowedSincLowpassFilter.ApplyLowpassFilter(tone, kernel);

        // Assert: away from the edges the output peak is more than 30 dB below the 0.5 input peak
        Assert.True(InteriorPeak(filtered) < 0.5 / 31.6, $"peak {InteriorPeak(filtered)}");
    }

    /// <summary>Proves a tone well inside the passband keeps its amplitude.</summary>
    [Fact]
    public void WindowedSincLowpassFilter_ApplyLowpassFilter_ToneInPassband_IsPreserved()
    {
        // Arrange
        var kernel = WindowedSincLowpassFilter.BuildLowpassKernel(1.0 / 3.0, WindowedSincLowpassFilter.DownsamplingFilterTapCount);
        var tone = Tone(1000);

        // Act
        var filtered = WindowedSincLowpassFilter.ApplyLowpassFilter(tone, kernel);

        // Assert: the interior peak stays within 0.02 of the 0.5 input peak
        Assert.InRange(InteriorPeak(filtered), 0.48, 0.52);
    }

    /// <summary>Proves the kernel passes a constant signal at unity gain, including at the edges.</summary>
    [Fact]
    public void WindowedSincLowpassFilter_ApplyLowpassFilter_ConstantSignal_KeepsUnityGain()
    {
        // Arrange
        var kernel = WindowedSincLowpassFilter.BuildLowpassKernel(0.5, WindowedSincLowpassFilter.DownsamplingFilterTapCount);
        var constant = Enumerable.Repeat(0.25f, 200).ToArray();

        // Act
        var filtered = WindowedSincLowpassFilter.ApplyLowpassFilter(constant, kernel);

        // Assert
        Assert.Equal(constant.Length, filtered.Length);
        Assert.All(filtered, sample => Assert.Equal(0.25f, sample, 1e-4f));
    }

    /// <summary>Proves the filter output at an interior sample equals the dot product of the kernel and its input window.</summary>
    [Fact]
    public void WindowedSincLowpassFilter_ApplyLowpassFilter_InteriorSample_EqualsKernelDotProduct()
    {
        // Arrange
        var kernel = WindowedSincLowpassFilter.BuildLowpassKernel(0.5, 5);
        float[] input = [1f, -2f, 3f, 0.5f, -1f, 2f, 4f, -3f, 0.25f, 1.5f];
        const int Index = 4;

        // Act
        var filtered = WindowedSincLowpassFilter.ApplyLowpassFilter(input, kernel);

        // Assert: the expected value is summed by hand from the same five-sample window
        var expected = 0.0;
        for (var tap = 0; tap < kernel.Length; tap++)
        {
            expected += (double)kernel[tap] * input[Index - 2 + tap];
        }

        Assert.Equal(expected, filtered[Index], 1e-5);
    }

    /// <summary>Generates a 0.5-amplitude sine tone at the source rate, one second long.</summary>
    /// <param name="frequency">The tone frequency in hertz.</param>
    /// <returns>The tone samples.</returns>
    private static float[] Tone(double frequency) =>
        [.. Enumerable.Range(0, SourceRate).Select(i => (float)(0.5 * Math.Sin(2 * Math.PI * frequency * i / SourceRate)))];

    /// <summary>Gets the largest absolute value in the middle half of a signal.</summary>
    /// <param name="samples">The signal.</param>
    /// <returns>The peak magnitude away from the edges.</returns>
    private static double InteriorPeak(float[] samples)
    {
        var peak = 0.0;
        for (var i = samples.Length / 4; i < samples.Length * 3 / 4; i++)
        {
            peak = Math.Max(peak, Math.Abs(samples[i]));
        }

        return peak;
    }
}
