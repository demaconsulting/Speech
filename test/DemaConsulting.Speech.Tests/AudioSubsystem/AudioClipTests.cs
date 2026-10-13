using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="AudioClip"/>.
/// </summary>
public sealed class AudioClipTests
{
    /// <summary>Proves the clip's derived members.</summary>
    [Fact]
    public void AudioClip_DerivedMembers_AreConsistent()
    {
        // Arrange
        var clip = new AudioClip([1f, -1f, 0.5f, 0.5f, 2f, -2f, 0f, float.NaN], 4, 2, 16);

        // Act
        var mono = clip.ToMono();
        var pcm = clip.ToPcm16();

        // Assert
        Assert.Equal(4, clip.FrameCount);
        Assert.Equal(1.0, clip.Duration);
        Assert.Equal([0f, 0.5f, 0f, float.NaN], mono);
        Assert.Equal(8, pcm.Length);
        Assert.Equal(32767, pcm[0]);
        Assert.Equal(-32768, pcm[1]);
        Assert.Equal(32767, pcm[4]);
        Assert.Equal(-32768, pcm[5]);
        Assert.Equal(0, pcm[7]);
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

    /// <summary>Proves <see cref="AudioClip"/> rejects a null sample array and non-positive rates or channel counts, including through <c>with</c>.</summary>
    [Fact]
    public void AudioClip_InvalidValues_Throw()
    {
        // Arrange
        var valid = new AudioClip([0f, 0f], 8000, 1, 16);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new AudioClip(null!, 8000, 1, 16));
        Assert.Equal("SampleRate", Assert.Throws<ArgumentOutOfRangeException>(() => new AudioClip([0f], 0, 1, 16)).ParamName);
        Assert.Equal("SampleRate", Assert.Throws<ArgumentOutOfRangeException>(() => new AudioClip([0f], -1, 1, 16)).ParamName);
        Assert.Equal("ChannelCount", Assert.Throws<ArgumentOutOfRangeException>(() => new AudioClip([0f], 8000, 0, 16)).ParamName);
        Assert.Equal("ChannelCount", Assert.Throws<ArgumentOutOfRangeException>(() => new AudioClip([0f], 8000, -2, 16)).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => valid with { SampleRate = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => valid with { ChannelCount = 0 });
        Assert.Throws<ArgumentNullException>(() => valid with { Samples = null! });
        Assert.Equal(16000, (valid with { SampleRate = 16000 }).SampleRate);
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
