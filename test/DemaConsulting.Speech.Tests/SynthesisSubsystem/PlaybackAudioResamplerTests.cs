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
        // Arrange: a two-sample ramp, upsampled to four times the rate
        float[] samples = [0.0f, 1.0f];

        // Act
        var result = PlaybackAudioResampler.Resample(samples, sourceSampleRate: 8000, targetSampleRate: 16000);

        // Assert: twice as many output samples
        Assert.Equal(4, result.Length);
    }

    /// <summary>
    ///     Proves that downsampling reduces the sample count proportionally.
    /// </summary>
    [Fact]
    public void PlaybackAudioResampler_Resample_Downsample_ReducesSampleCount()
    {
        // Arrange
        float[] samples = [0.0f, 0.25f, 0.5f, 0.75f];

        // Act
        var result = PlaybackAudioResampler.Resample(samples, sourceSampleRate: 16000, targetSampleRate: 8000);

        // Assert
        Assert.Equal(2, result.Length);
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
        int sourceSampleRate, int targetSampleRate)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlaybackAudioResampler.Resample([0.1f], sourceSampleRate, targetSampleRate));
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
        Assert.Throws<ArgumentOutOfRangeException>(() => PlaybackAudioResampler.UpmixToChannels([0.1f], channelCount: 0));
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
        var resampler = new PlaybackAudioResampler(sourceSampleRate: 8000, targetSampleRate: 16000, targetChannelCount: 2);
        float[] samples = [0.5f, 1.0f];

        // Act
        var result = resampler.Convert(samples);

        // Assert: resampled to 4 mono samples, then interleaved to 8 stereo samples
        Assert.Equal(8, result.Length);
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
        int sourceSampleRate, int targetSampleRate, int targetChannelCount)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PlaybackAudioResampler(sourceSampleRate, targetSampleRate, targetChannelCount));
    }
}
