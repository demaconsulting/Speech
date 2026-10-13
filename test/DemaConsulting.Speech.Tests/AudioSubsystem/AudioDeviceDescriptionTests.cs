using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="AudioDeviceDescription"/>.
/// </summary>
public sealed class AudioDeviceDescriptionTests
{
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
