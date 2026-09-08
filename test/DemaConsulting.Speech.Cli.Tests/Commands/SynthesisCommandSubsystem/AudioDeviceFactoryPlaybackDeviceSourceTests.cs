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
///     hardware-detection behavior - so <c>SpeakCommand.Run(Context)</c>'s real playback-device
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
    public void AudioDeviceFactoryPlaybackDeviceSource_PlaybackProbe_ForwardsToFactory()
    {
        var factory = new AudioDeviceFactory();
        var source = new AudioDeviceFactoryPlaybackDeviceSource(factory);

        Assert.Same(factory.PlaybackProbe, source.PlaybackProbe);
    }

    /// <summary>
    ///     Test that CreatePlaybackDevice forwards to the composed factory's own
    ///     CreatePlaybackDevice, for both the default selection and a named selection - proving
    ///     the adapter never substitutes its own resolution logic, regardless of whether real
    ///     playback hardware happens to be present on the machine running this test.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("some-device-name")]
    public void AudioDeviceFactoryPlaybackDeviceSource_CreatePlaybackDevice_ForwardsToFactory(string? deviceName)
    {
        var factory = new AudioDeviceFactory();
        var source = new AudioDeviceFactoryPlaybackDeviceSource(factory);
        var selection = deviceName is null ? null : new AudioDeviceSelection(deviceName);

        var expected = factory.CreatePlaybackDevice(selection);
        var actual = source.CreatePlaybackDevice(selection);

        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.IsAvailable, actual.IsAvailable);
    }

    /// <summary>Test that a null factory is rejected.</summary>
    [Fact]
    public void AudioDeviceFactoryPlaybackDeviceSource_NullFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AudioDeviceFactoryPlaybackDeviceSource(null!));
    }
}
