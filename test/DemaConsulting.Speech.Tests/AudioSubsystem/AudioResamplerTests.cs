using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="AudioResampler"/>.
/// </summary>
public sealed class AudioResamplerTests
{
    /// <summary>Proves DownMix averages channels and passes mono through.</summary>
    [Fact]
    public void AudioResampler_DownMix_AveragesChannels()
    {
        // Arrange
        float[] stereo = [1f, 0f, 0.5f, 0.5f];

        // Act / Assert
        Assert.Equal([0.5f, 0.5f], AudioResampler.DownMix(stereo, 2));
        Assert.Equal(stereo, AudioResampler.DownMix(stereo, 1));
    }

    /// <summary>Proves DownMix drops a trailing partial frame rather than reading past the data.</summary>
    [Fact]
    public void AudioResampler_DownMix_PartialTrailingFrame_IsDropped()
    {
        // Arrange: one whole stereo frame plus one stray sample
        float[] ragged = [1f, 0f, 0.75f];

        // Act
        var mixed = AudioResampler.DownMix(ragged, 2);

        // Assert
        Assert.Equal([0.5f], mixed);
    }

    /// <summary>Proves Resample preserves the length ratio and a 440 Hz tone's frequency when changing rate.</summary>
    [Theory]
    [InlineData(44100, 24000)]
    [InlineData(16000, 24000)]
    [InlineData(48000, 16000)]
    public void AudioResampler_Resample_ChangesRateKeepingTone(int inputRate, int outputRate)
    {
        // Arrange
        var tone = Tone(inputRate, 0.5, 440);

        // Act
        var result = AudioResampler.Resample(tone, inputRate, outputRate);

        // Assert: length and a close match to the ideal tone away from the edges
        Assert.InRange(result.Length, (int)(0.5 * outputRate) - 1, (int)(0.5 * outputRate) + 1);
        var worst = 0.0;
        for (var i = result.Length / 4; i < result.Length * 3 / 4; i++)
        {
            worst = Math.Max(worst, Math.Abs(result[i] - (0.5 * Math.Sin(2 * Math.PI * 440 * i / outputRate))));
        }

        Assert.True(worst < 0.02, $"max error {worst}");
    }

    /// <summary>
    ///     Proves the downsampling convolution preserves the level of a constant signal, a direct
    ///     numeric check of the kernel dot product (the kernel has unity gain at zero frequency).
    /// </summary>
    [Fact]
    public void AudioResampler_Resample_ConstantSignalDownsampled_PreservesLevel()
    {
        // Arrange
        var input = Enumerable.Repeat(0.5f, 16000).ToArray();

        // Act
        var result = AudioResampler.Resample(input, 16000, 8000);

        // Assert: away from the edges every output sample equals the constant input level
        for (var i = result.Length / 4; i < result.Length * 3 / 4; i++)
        {
            Assert.Equal(0.5f, result[i], 1e-3f);
        }
    }

    /// <summary>Proves equal rates return a distinct copy and empty input yields empty output.</summary>
    [Fact]
    public void AudioResampler_Resample_SameRateOrEmpty_AreTrivial()
    {
        // Arrange
        float[] input = [0.1f, 0.2f, 0.3f];

        // Act
        var same = AudioResampler.Resample(input, 8000, 8000);

        // Assert
        Assert.Equal(input, same);
        Assert.NotSame(input, same);
        Assert.Empty(AudioResampler.Resample([], 8000, 16000));
    }

    /// <summary>Proves ToMonoResampled combines down-mixing and resampling.</summary>
    [Fact]
    public void AudioResampler_ToMonoResampled_StereoClip_ProducesMonoAtTargetRate()
    {
        // Arrange
        var mono = Tone(16000, 0.25, 300);
        var interleaved = mono.SelectMany(v => new[] { v, v }).ToArray();
        var clip = new AudioClip(interleaved, 16000, 2, 16);

        // Act
        var result = AudioResampler.ToMonoResampled(clip, 24000);

        // Assert
        Assert.InRange(result.Length, 5999, 6001);
        Assert.Contains(result, v => Math.Abs(v) > 0.1f);
    }

    /// <summary>Proves extreme down-sampling of a constant signal keeps unity gain at every output sample, edges included.</summary>
    [Theory]
    [InlineData(48000, 100)]
    [InlineData(48000, 1000)]
    [InlineData(44100, 8000)]
    public void AudioResampler_Resample_ConstantSignalExtremeDownsample_KeepsUnityGainAtEdges(int inputRate, int outputRate)
    {
        // Arrange
        var input = Enumerable.Repeat(1f, inputRate / 2).ToArray();

        // Act
        var output = AudioResampler.Resample(input, inputRate, outputRate);

        // Assert
        Assert.NotEmpty(output);
        Assert.All(output, sample => Assert.InRange(sample, 0.99f, 1.01f));
    }

    /// <summary>Proves DownMix rejects a non-positive channel count.</summary>
    [Fact]
    public void AudioResampler_DownMix_NonPositiveChannelCount_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioResampler.DownMix([0.1f, 0.2f], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioResampler.DownMix([0.1f, 0.2f], -1));
    }

    /// <summary>Proves Resample rejects a non-positive input or output rate.</summary>
    [Fact]
    public void AudioResampler_Resample_NonPositiveRates_Throw()
    {
        // Arrange
        float[] input = [0.1f, 0.2f, 0.3f];

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioResampler.Resample(input, 0, 16000));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioResampler.Resample(input, 8000, 0));
    }

    /// <summary>Proves ToMonoResampled rejects a null clip.</summary>
    [Fact]
    public void AudioResampler_ToMonoResampled_NullClip_Throws() =>
        Assert.Throws<ArgumentNullException>(() => AudioResampler.ToMonoResampled(null!, 24000));

    /// <summary>Proves ToMonoResampled rejects a non-positive target rate.</summary>
    [Fact]
    public void AudioResampler_ToMonoResampled_NonPositiveTargetRate_Throws()
    {
        // Arrange
        var clip = new AudioClip([0.1f, 0.2f], 16000, 1, 16);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioResampler.ToMonoResampled(clip, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioResampler.ToMonoResampled(clip, -1));
    }

    /// <summary>Generates a sine tone.</summary>
    private static float[] Tone(int rate, double seconds, double frequency) =>
        [.. Enumerable.Range(0, (int)(rate * seconds)).Select(i => (float)(0.5 * Math.Sin(2 * Math.PI * frequency * i / rate)))];
}
