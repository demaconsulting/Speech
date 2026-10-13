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

        // Act & Assert
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>
    ///     Proves that a description differing in any single property compares unequal.
    /// </summary>
    [Fact]
    public void AudioDeviceDescription_Equality_AnyDifferingValue_AreNotEqual()
    {
        // Arrange
        var baseline = new AudioDeviceDescription("Mic", AudioDeviceDirection.Capture, 2, 48000);

        // Act & Assert
        Assert.NotEqual(baseline, baseline with { Name = "Other" });
        Assert.NotEqual(baseline, baseline with { Direction = AudioDeviceDirection.Playback });
        Assert.NotEqual(baseline, baseline with { ChannelCount = 1 });
        Assert.NotEqual(baseline, baseline with { SampleRate = 16000 });
    }
}
