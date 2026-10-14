using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="AudioClip"/>.
/// </summary>
public sealed class AudioClipTests
{
    /// <summary>Proves the frame count and duration are derived from the samples, rate and channels.</summary>
    [Fact]
    public void AudioClip_DerivedMembers_FrameCountAndDuration()
    {
        // Arrange
        var clip = new AudioClip([1f, -1f, 0.5f, 0.5f, 2f, -2f, 0f, float.NaN], 4, 2, 16);

        // Act
        var frames = clip.FrameCount;
        var duration = clip.Duration;

        // Assert
        Assert.Equal(4, frames);
        Assert.Equal(1.0, duration);
    }

    /// <summary>Proves the mono down-mix of a multi-channel clip.</summary>
    [Fact]
    public void AudioClip_DerivedMembers_MonoDownMix()
    {
        // Arrange
        var clip = new AudioClip([1f, -1f, 0.5f, 0.5f, 2f, -2f, 0f, float.NaN], 4, 2, 16);

        // Act
        var mono = clip.ToMono();

        // Assert
        Assert.Equal([0f, 0.5f, 0f, float.NaN], mono);
    }

    /// <summary>Proves the 16-bit PCM conversion of a multi-channel clip.</summary>
    [Fact]
    public void AudioClip_DerivedMembers_Pcm16Conversion()
    {
        // Arrange
        var clip = new AudioClip([1f, -1f, 0.5f, 0.5f, 2f, -2f, 0f, float.NaN], 4, 2, 16);

        // Act
        var pcm = clip.ToPcm16();

        // Assert
        Assert.Equal([32767, -32768, 16384, 16384, 32767, -32768, 0, 0], pcm.Select(v => (int)v));
    }

    /// <summary>Proves a sample count that is not a multiple of the channel count truncates the frame count and drops the partial frame in the mono down-mix.</summary>
    [Fact]
    public void AudioClip_NonMultipleFrameCount_TruncatesToWholeFrames()
    {
        // Arrange: three samples in two channels is one whole frame plus one stray sample
        var clip = new AudioClip([1f, 0f, 0.5f], 4, 2, 16);

        // Act
        var mono = clip.ToMono();

        // Assert
        Assert.Equal(1, clip.FrameCount);
        Assert.Equal(0.25, clip.Duration);
        Assert.Equal([0.5f], mono);
        Assert.Equal(3, clip.ToPcm16().Length);
    }

    /// <summary>Proves BitsPerSample is informational: it is not validated, even through a <c>with</c> expression.</summary>
    [Fact]
    public void AudioClip_BitsPerSample_IsNotValidated()
    {
        // Arrange
        var clip = new AudioClip([0f], 8000, 1, 16);

        // Act
        var changed = clip with { BitsPerSample = -1 };

        // Assert
        Assert.Equal(-1, changed.BitsPerSample);
        Assert.Equal(0, new AudioClip([0f], 8000, 1, 0).BitsPerSample);
    }

    /// <summary>Proves ToMono averages the channels of each frame.</summary>
    [Fact]
    public void AudioClip_ToMono_AveragesChannels()
    {
        // Arrange
        var clip = new AudioClip([1f, 0f, 0.5f, 0.5f], 4, 2, 16);

        // Act / Assert
        Assert.Equal([0.5f, 0.5f], clip.ToMono());
    }

    /// <summary>Proves ToMono of a mono clip is a copy, so callers may modify it without altering the clip.</summary>
    [Fact]
    public void AudioClip_ToMono_MonoClip_ReturnsIndependentCopy()
    {
        // Arrange
        var clip = new AudioClip([0.25f, -0.25f], 8000, 1, 16);

        // Act
        var mono = clip.ToMono();
        mono[0] = 0.9f;

        // Assert
        Assert.Equal([0.25f, -0.25f], clip.Samples);
    }

    /// <summary>Proves a NaN sample propagates through the mono down-mix (documented as incidental behavior).</summary>
    [Fact]
    public void AudioClip_ToMono_NaNSample_PropagatesToFrame()
    {
        // Arrange
        var clip = new AudioClip([float.NaN, 1f, 0.5f, 0.5f], 8000, 2, 16);

        // Act
        var mono = clip.ToMono();

        // Assert
        Assert.True(float.IsNaN(mono[0]));
        Assert.Equal(0.5f, mono[1]);
    }

    /// <summary>Proves ToPcm16 clamps out-of-range samples to the 16-bit range.</summary>
    [Fact]
    public void AudioClip_ToPcm16_ClampsOutOfRangeSamples()
    {
        // Arrange
        var clip = new AudioClip([2f, -2f, 0f], 8000, 1, 16);

        // Act
        var pcm = clip.ToPcm16();

        // Assert
        Assert.Equal([32767, -32768, 0], pcm.Select(v => (int)v));
    }

    /// <summary>Proves ToPcm16 scales by 32768 with rounding: +1.0 clamps to 32767 and -1.0 maps to -32768.</summary>
    [Fact]
    public void AudioClip_ToPcm16_ScalesWithRounding()
    {
        // Arrange
        var clip = new AudioClip([1f, -1f, 0.5f, 1f / 65536f, -1f / 65536f], 8000, 1, 16);

        // Act
        var pcm = clip.ToPcm16();

        // Assert: 0.5 -> 16384, and half a step rounds to even (0)
        Assert.Equal([32767, -32768, 16384, 0, 0], pcm.Select(v => (int)v));
    }

    /// <summary>Proves the constructor rejects a null sample array.</summary>
    [Fact]
    public void AudioClip_InvalidValues_NullSamples_Throws()
    {
        // Arrange / Act
        var error = Record.Exception(() => new AudioClip(null!, 8000, 1, 16));

        // Assert
        Assert.IsType<ArgumentNullException>(error);
    }

    /// <summary>Proves the constructor rejects a non-positive sample rate and names the parameter.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AudioClip_InvalidValues_SampleRate_Throws(int rate)
    {
        // Arrange / Act
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new AudioClip([0f], rate, 1, 16));

        // Assert
        Assert.Equal("SampleRate", error.ParamName);
    }

    /// <summary>Proves the constructor rejects a non-positive channel count and names the parameter.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void AudioClip_InvalidValues_ChannelCount_Throws(int channels)
    {
        // Arrange / Act
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new AudioClip([0f], 8000, channels, 16));

        // Assert
        Assert.Equal("ChannelCount", error.ParamName);
    }

    /// <summary>Proves a <c>with</c> expression rejects a zero sample rate.</summary>
    [Fact]
    public void AudioClip_InvalidValues_WithSampleRate_Throws()
    {
        // Arrange
        var valid = new AudioClip([0f, 0f], 8000, 1, 16);

        // Act
        var error = Record.Exception(() => valid with { SampleRate = 0 });

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(error);
    }

    /// <summary>Proves a <c>with</c> expression rejects a zero channel count.</summary>
    [Fact]
    public void AudioClip_InvalidValues_WithChannelCount_Throws()
    {
        // Arrange
        var valid = new AudioClip([0f, 0f], 8000, 1, 16);

        // Act
        var error = Record.Exception(() => valid with { ChannelCount = 0 });

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(error);
    }

    /// <summary>Proves a <c>with</c> expression rejects null samples.</summary>
    [Fact]
    public void AudioClip_InvalidValues_WithNullSamples_Throws()
    {
        // Arrange
        var valid = new AudioClip([0f, 0f], 8000, 1, 16);

        // Act
        var error = Record.Exception(() => valid with { Samples = null! });

        // Assert
        Assert.IsType<ArgumentNullException>(error);
    }

    /// <summary>Proves a <c>with</c> expression accepts a valid sample rate.</summary>
    [Fact]
    public void AudioClip_With_ValidSampleRate_Applies()
    {
        // Arrange
        var valid = new AudioClip([0f, 0f], 8000, 1, 16);

        // Act
        var changed = valid with { SampleRate = 16000 };

        // Assert
        Assert.Equal(16000, changed.SampleRate);
    }

    /// <summary>Proves an empty sample array is a valid, zero-length clip.</summary>
    [Fact]
    public void AudioClip_EmptySamples_HasZeroDuration()
    {
        // Arrange / Act
        var clip = new AudioClip([], 8000, 2, 16);

        // Assert
        Assert.Equal(0, clip.FrameCount);
        Assert.Equal(0.0, clip.Duration);
    }
}
