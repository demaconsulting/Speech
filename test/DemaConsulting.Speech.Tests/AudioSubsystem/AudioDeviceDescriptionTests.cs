using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="AudioDeviceDescription"/>.
/// </summary>
public sealed class AudioDeviceDescriptionTests
{
    /// <summary>
    ///     Proves that a description exposes the name, direction, channel count and sample rate it was built with.
    /// </summary>
    [Fact]
    public void AudioDeviceDescription_Constructor_ExposesAllFields()
    {
        // Arrange / Act
        var description = new AudioDeviceDescription("Mic", AudioDeviceDirection.Playback, 6, 44100);

        // Assert
        Assert.Equal("Mic", description.Name);
        Assert.Equal(AudioDeviceDirection.Playback, description.Direction);
        Assert.Equal(6, description.ChannelCount);
        Assert.Equal(44100, description.SampleRate);
    }

    /// <summary>
    ///     Proves that two descriptions with identical property values compare equal.
    /// </summary>
    [Fact]
    public void AudioDeviceDescription_Equality_IdenticalValues_AreEqual()
    {
        // Arrange
        var first = new AudioDeviceDescription("Mic", AudioDeviceDirection.Capture, 2, 48000);
        var second = new AudioDeviceDescription("Mic", AudioDeviceDirection.Capture, 2, 48000);

        // Act
        var hashesEqual = first.GetHashCode() == second.GetHashCode();

        // Assert
        Assert.Equal(first, second);
        Assert.True(hashesEqual);
    }

    /// <summary>
    ///     Proves that a description differing in any single property compares unequal.
    /// </summary>
    [Fact]
    public void AudioDeviceDescription_Equality_AnyDifferingValue_AreNotEqual()
    {
        // Arrange
        var baseline = new AudioDeviceDescription("Mic", AudioDeviceDirection.Capture, 2, 48000);

        // Act
        var withName = baseline with { Name = "Other" };
        var withDirection = baseline with { Direction = AudioDeviceDirection.Playback };
        var withChannels = baseline with { ChannelCount = 1 };
        var withRate = baseline with { SampleRate = 16000 };

        // Assert
        Assert.NotEqual(baseline, withName);
        Assert.NotEqual(baseline, withDirection);
        Assert.NotEqual(baseline, withChannels);
        Assert.NotEqual(baseline, withRate);
    }
}
