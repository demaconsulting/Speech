// Copyright (c) DEMA Consulting
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Cli.Commands.SynthesisCommandSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.SynthesisCommandSubsystem;

/// <summary>
///     Unit tests for <see cref="AudioDeviceFactoryPlaybackDeviceSource"/>, proving it forwards
///     every call to a real, composed <see cref="AudioDeviceFactory"/> unchanged - including its
///     hardware-detection behavior - so <c>SpeakCommand.RunAsync(Context)</c>'s real playback-device
///     resolution is provably unaffected by the <see cref="ICliPlaybackDeviceSource"/> seam
///     introduced for <c>SpeakCommandTests</c>' own deterministic, hardware-independent tests.
/// </summary>
/// <remarks>
///     These tests deliberately do not assert on whether real playback hardware is actually
///     present (that varies by machine and is exactly the condition that must not affect these
///     assertions): they only prove the adapter is a pure pass-through, by comparing its result to
///     the real <see cref="AudioDeviceFactory"/>'s own result for the same call.
/// </remarks>
public sealed class AudioDeviceFactoryPlaybackDeviceSourceTests
{
    /// <summary>Test that PlaybackProbe forwards to the composed factory's own PlaybackProbe.</summary>
    [Fact]
    public void AudioDeviceFactoryPlaybackDeviceSource_PlaybackProbe_ReturnsInjectedProbeIdentity()
    {
        // Arrange - a factory built over an explicitly injected probe (deterministic, no hardware)
        var probe = new StubPlaybackProbe([]);
        var factory = new AudioDeviceFactory(playbackProbe: probe);
        var source = new AudioDeviceFactoryPlaybackDeviceSource(factory);

        // Act
        var actual = source.PlaybackProbe;

        // Assert
        Assert.Same(probe, actual);
    }

    /// <summary>
    ///     Test that CreatePlaybackDevice for the default selection resolves to the honestly
    ///     unavailable device when the injected probe reports no devices, independent of whether
    ///     real playback hardware is present on the machine running this test.
    /// </summary>
    [Fact]
    public void AudioDeviceFactoryPlaybackDeviceSource_CreatePlaybackDevice_EmptyProbeDefault_ReturnsUnavailable()
    {
        // Arrange
        var factory = new AudioDeviceFactory(playbackProbe: new StubPlaybackProbe([]));
        var source = new AudioDeviceFactoryPlaybackDeviceSource(factory);

        // Act
        var device = source.CreatePlaybackDevice(null);

        // Assert
        Assert.False(device.IsAvailable);
        Assert.Same(UnavailableAudioPlaybackDevice.Instance, device);
    }

    /// <summary>
    ///     Test that CreatePlaybackDevice forwards a named selection that the injected probe does
    ///     not know to the factory, which yields the unavailable device.
    /// </summary>
    [Fact]
    public void AudioDeviceFactoryPlaybackDeviceSource_CreatePlaybackDevice_UnknownNamedSelection_ReturnsUnavailable()
    {
        // Arrange
        var known = new AudioDeviceDescription("known-device", AudioDeviceDirection.Playback, 2, 48000);
        var factory = new AudioDeviceFactory(playbackProbe: new StubPlaybackProbe([known]));
        var source = new AudioDeviceFactoryPlaybackDeviceSource(factory);

        // Act
        var device = source.CreatePlaybackDevice(new AudioDeviceSelection("some-other-device"));

        // Assert
        Assert.False(device.IsAvailable);
    }

    /// <summary>
    ///     Test that CreatePlaybackDevice returns the same kind of device the composed factory
    ///     itself would return for the same selection.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("known-device")]
    public void AudioDeviceFactoryPlaybackDeviceSource_CreatePlaybackDevice_MatchesFactoryResult(string? deviceName)
    {
        // Arrange
        var known = new AudioDeviceDescription("known-device", AudioDeviceDirection.Playback, 2, 48000);
        var factory = new AudioDeviceFactory(playbackProbe: new StubPlaybackProbe([known]));
        var source = new AudioDeviceFactoryPlaybackDeviceSource(factory);
        var selection = deviceName is null ? null : new AudioDeviceSelection(deviceName);

        // Act
        var expected = factory.CreatePlaybackDevice(selection);
        var actual = source.CreatePlaybackDevice(selection);

        // Assert
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.IsAvailable, actual.IsAvailable);
    }

    /// <summary>Test that a null factory is rejected.</summary>
    [Fact]
    public void AudioDeviceFactoryPlaybackDeviceSource_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new AudioDeviceFactoryPlaybackDeviceSource(null!));
    }

    /// <summary>A deterministic playback probe returning a fixed device list.</summary>
    /// <param name="devices">The devices <see cref="Enumerate"/> reports.</param>
    private sealed class StubPlaybackProbe(IReadOnlyList<AudioDeviceDescription> devices) : IAudioPlaybackDeviceProbe
    {
        /// <inheritdoc/>
        public IReadOnlyList<AudioDeviceDescription> Enumerate() => devices;
    }
}
