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
using DemaConsulting.Speech.Cli.Cli;
using DemaConsulting.Speech.Cli.Commands.DeviceCommandsSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.DeviceCommandsSubsystem;

/// <summary>
///     Unit tests for <see cref="DevicesTestCommand"/>'s hardware-independent logic: argument
///     parsing, device-not-found handling, and tone-generation math. The actual device creation
///     and audio I/O path depends on the real native audio runtime and is not exercised here;
///     see <c>docs/verification/speech-cli/device-commands-subsystem.md</c> for why that path is
///     manual/local verification only.
/// </summary>
[Collection("Sequential")]
public sealed class DevicesTestCommandTests
{
    private static readonly AudioDeviceDescription InputDevice =
        new("Fake Microphone", AudioDeviceDirection.Capture, 1, 16000);

    private static readonly AudioDeviceDescription OutputDevice =
        new("Fake Speakers", AudioDeviceDirection.Playback, 2, 48000);

    /// <summary>
    ///     Test that <c>devices test</c> with no flags parses to the default direction (output)
    ///     and no requested device name.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_NoFlags_DefaultsToOutputDirection()
    {
        var options = DevicesTestCommand.ParseArguments(["test"]);

        Assert.Null(options.DeviceName);
        Assert.Equal(AudioDeviceDirection.Playback, options.Direction);
    }

    /// <summary>
    ///     Test that <c>--direction input</c> parses to the capture direction.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_DirectionInput_ParsesToCaptureDirection()
    {
        var options = DevicesTestCommand.ParseArguments(["test", "--direction", "input"]);

        Assert.Equal(AudioDeviceDirection.Capture, options.Direction);
    }

    /// <summary>
    ///     Test that <c>--direction output</c> parses to the playback direction.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_DirectionOutput_ParsesToPlaybackDirection()
    {
        var options = DevicesTestCommand.ParseArguments(["test", "--direction", "output"]);

        Assert.Equal(AudioDeviceDirection.Playback, options.Direction);
    }

    /// <summary>
    ///     Test that <c>--device &lt;name&gt;</c> parses the requested device name.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_DeviceFlag_ParsesDeviceName()
    {
        var options = DevicesTestCommand.ParseArguments(["test", "--device", "My Speakers"]);

        Assert.Equal("My Speakers", options.DeviceName);
    }

    /// <summary>
    ///     Test that a missing sub-action throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_MissingSubAction_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => DevicesTestCommand.ParseArguments([]));
    }

    /// <summary>
    ///     Test that a sub-action other than <c>test</c> throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_UnknownSubAction_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => DevicesTestCommand.ParseArguments(["bogus"]));
    }

    /// <summary>
    ///     Test that an invalid <c>--direction</c> value throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_InvalidDirectionValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => DevicesTestCommand.ParseArguments(["test", "--direction", "sideways"]));
    }

    /// <summary>
    ///     Test that an unsupported flag throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_UnsupportedFlag_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => DevicesTestCommand.ParseArguments(["test", "--bogus"]));
    }

    /// <summary>
    ///     Test that a flag missing its value throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_DeviceFlagMissingValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => DevicesTestCommand.ParseArguments(["test", "--device"]));
    }

    /// <summary>
    ///     Test that a <see langword="null"/> device name resolves to the system default
    ///     (a <see langword="null"/> selection) without needing any known devices.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ResolveDeviceSelectionOrThrow_NullDeviceName_ReturnsNull()
    {
        var selection = DevicesTestCommand.ResolveDeviceSelectionOrThrow([], null, "playback");

        Assert.Null(selection);
    }

    /// <summary>
    ///     Test that a device name matching a known device resolves to a selection for it.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ResolveDeviceSelectionOrThrow_KnownDeviceName_ReturnsSelection()
    {
        var selection = DevicesTestCommand.ResolveDeviceSelectionOrThrow([OutputDevice], OutputDevice.Name, "playback");

        Assert.NotNull(selection);
        Assert.Equal(OutputDevice.Name, selection.DeviceName);
    }

    /// <summary>
    ///     Test that a device name not matching any known device throws a clean
    ///     <see cref="ArgumentException"/> naming the device, rather than proceeding to device
    ///     creation.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ResolveDeviceSelectionOrThrow_UnknownDeviceName_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            DevicesTestCommand.ResolveDeviceSelectionOrThrow([InputDevice], "does-not-exist", "capture"));

        Assert.Contains("does-not-exist", exception.Message);
        Assert.Contains("list-devices", exception.Message);
    }

    /// <summary>
    ///     Test that a <see langword="null"/> known-devices list is rejected.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ResolveDeviceSelectionOrThrow_NullKnownDevices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DevicesTestCommand.ResolveDeviceSelectionOrThrow(null!, "any", "playback"));
    }

    /// <summary>
    ///     Test that the generated tone has the expected sample count for the requested duration,
    ///     sample rate, and channel count.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_GenerateToneSamples_ReturnsExpectedSampleCount()
    {
        var samples = DevicesTestCommand.GenerateToneSamples(sampleRate: 1000, channelCount: 2, durationSeconds: 1.0, frequencyHz: 100.0);

        Assert.Equal(2000, samples.Length);
    }

    /// <summary>
    ///     Test that every generated sample stays within the normalized <c>[-1.0, 1.0]</c> range.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_GenerateToneSamples_AllSamplesWithinNormalizedRange()
    {
        var samples = DevicesTestCommand.GenerateToneSamples(sampleRate: 44100, channelCount: 1, durationSeconds: 0.5, frequencyHz: 440.0);

        Assert.All(samples, sample => Assert.InRange(sample, -1.0f, 1.0f));
    }

    /// <summary>
    ///     Test that every channel carries an identical value for a given frame, since the tone
    ///     is generated identically across channels.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_GenerateToneSamples_IdenticalAcrossChannels()
    {
        var samples = DevicesTestCommand.GenerateToneSamples(sampleRate: 8000, channelCount: 2, durationSeconds: 0.1, frequencyHz: 440.0);

        for (var frame = 0; frame < samples.Length / 2; frame++)
        {
            Assert.Equal(samples[frame * 2], samples[(frame * 2) + 1]);
        }
    }

    /// <summary>
    ///     Test that a non-positive sample rate is rejected.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_GenerateToneSamples_NonPositiveSampleRate_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DevicesTestCommand.GenerateToneSamples(sampleRate: 0, channelCount: 1, durationSeconds: 1.0, frequencyHz: 440.0));
    }

    /// <summary>
    ///     Test that an unknown requested device throws a clean <see cref="ArgumentException"/>
    ///     before any device is created, using a real <see cref="AudioDeviceFactory"/> composed
    ///     over fake probes via its public constructor.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_Run_UnknownPlaybackDevice_ThrowsArgumentException()
    {
        var context = Context.Create(["devices", "test", "--device", "does-not-exist", "--direction", "output"]);
        var factory = new AudioDeviceFactory(new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe([OutputDevice]));

        var exception = Assert.Throws<ArgumentException>(() => DevicesTestCommand.Run(context, factory));
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>
    ///     Test that an unknown requested capture device throws a clean
    ///     <see cref="ArgumentException"/> before any device is created.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_Run_UnknownCaptureDevice_ThrowsArgumentException()
    {
        var context = Context.Create(["devices", "test", "--device", "does-not-exist", "--direction", "input"]);
        var factory = new AudioDeviceFactory(new FakeAudioCaptureDeviceProbe([InputDevice]), new FakeAudioPlaybackDeviceProbe());

        var exception = Assert.Throws<ArgumentException>(() => DevicesTestCommand.Run(context, factory));
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>
    ///     Test that a <see langword="null"/> context is rejected.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_Run_NullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => DevicesTestCommand.Run(null!, new AudioDeviceFactory()));
    }

    /// <summary>
    ///     Test that a <see langword="null"/> factory is rejected.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_Run_NullFactory_ThrowsArgumentNullException()
    {
        var context = Context.Create(["devices", "test"]);

        Assert.Throws<ArgumentNullException>(() => DevicesTestCommand.Run(context, null!));
    }
}
