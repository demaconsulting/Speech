using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="AudioFormat"/>.
/// </summary>
public sealed class AudioFormatTests
{
    /// <summary>
    ///     Proves that a valid sample rate and channel count are exposed unchanged.
    /// </summary>
    [Fact]
    public void AudioFormat_Constructor_ValidValues_ExposesProperties()
    {
        // Arrange
        var sampleRate = 48000;
        var channelCount = 2;

        // Act
        var format = new AudioFormat(sampleRate, channelCount);

        // Assert
        Assert.Equal(sampleRate, format.SampleRate);
        Assert.Equal(channelCount, format.ChannelCount);
    }

    /// <summary>
    ///     Proves that <see cref="AudioFormat.Mono"/> creates a single-channel format at the
    ///     requested rate.
    /// </summary>
    [Fact]
    public void AudioFormat_Mono_ValidSampleRate_ReturnsMonoFormat()
    {
        // Arrange
        var sampleRate = 16000;

        // Act
        var format = AudioFormat.Mono(sampleRate);

        // Assert
        Assert.Equal(sampleRate, format.SampleRate);
        Assert.Equal(1, format.ChannelCount);
    }

    /// <summary>
    ///     Proves that a non-positive sample rate is rejected.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AudioFormat_Constructor_NonPositiveSampleRate_ThrowsArgumentOutOfRangeException(int sampleRate)
    {
        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioFormat(sampleRate, channelCount: 1));
    }

    /// <summary>
    ///     Proves that a non-positive channel count is rejected.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AudioFormat_Constructor_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException(int channelCount)
    {
        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioFormat(sampleRate: 16000, channelCount));
    }

    /// <summary>
    ///     Proves that <see cref="AudioFormat.Mono"/> rejects a non-positive sample rate with the
    ///     same validation as the constructor.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AudioFormat_Mono_NonPositiveSampleRate_ThrowsArgumentOutOfRangeException(int sampleRate)
    {
        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioFormat.Mono(sampleRate));
    }

    /// <summary>
    ///     Proves that equal values compare equal and differing values compare unequal.
    /// </summary>
    [Fact]
    public void AudioFormat_ValueEquality_EquivalentValuesCompareByValue()
    {
        // Arrange
        var left = new AudioFormat(16000, 1);
        var same = new AudioFormat(16000, 1);
        var different = new AudioFormat(48000, 2);

        // Act / Assert
        Assert.Equal(left, same);
        Assert.NotEqual(left, different);
    }
}
