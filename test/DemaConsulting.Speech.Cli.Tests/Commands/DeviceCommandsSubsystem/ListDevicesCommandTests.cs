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
///     Unit tests for <see cref="ListDevicesCommand"/>.
/// </summary>
[Collection("Sequential")]
public sealed class ListDevicesCommandTests
{
    private static readonly AudioDeviceDescription InputDevice =
        new("Fake Microphone", AudioDeviceDirection.Capture, 1, 16000);

    private static readonly AudioDeviceDescription OutputDevice =
        new("Fake Speakers", AudioDeviceDirection.Playback, 2, 48000);

    /// <summary>
    ///     Test that no filter lists both capture and playback devices as a table.
    /// </summary>
    [Fact]
    public void ListDevicesCommand_Run_NoFilter_ListsBothDirections()
    {
        // Arrange
        var context = CreateContext([]);
        var captureProbe = new FakeAudioCaptureDeviceProbe([InputDevice]);
        var playbackProbe = new FakeAudioPlaybackDeviceProbe([OutputDevice]);

        // Act
        var output = RunCapturingOutput(context, captureProbe, playbackProbe);

        // Assert
        Assert.Contains("Fake Microphone", output);
        Assert.Contains("Fake Speakers", output);
        Assert.Contains("Capture", output);
        Assert.Contains("Playback", output);
    }

    /// <summary>
    ///     Test that each listed device reports its own channel count and sample rate, using values
    ///     distinctive enough that they cannot appear in the output by coincidence.
    /// </summary>
    [Fact]
    public void ListDevicesCommand_Run_Devices_ReportChannelCountAndSampleRate()
    {
        // Arrange
        var context = CreateContext([]);
        var captureProbe = new FakeAudioCaptureDeviceProbe(
            [new AudioDeviceDescription("Distinct Microphone", AudioDeviceDirection.Capture, 7, 44123)]);
        var playbackProbe = new FakeAudioPlaybackDeviceProbe(
            [new AudioDeviceDescription("Distinct Speakers", AudioDeviceDirection.Playback, 5, 96321)]);

        // Act
        var output = RunCapturingOutput(context, captureProbe, playbackProbe);

        // Assert
        var microphoneLine = output.Split('\n').Single(line => line.Contains("Distinct Microphone"));
        var speakersLine = output.Split('\n').Single(line => line.Contains("Distinct Speakers"));
        Assert.Contains("7", microphoneLine);
        Assert.Contains("44123", microphoneLine);
        Assert.Contains("5", speakersLine);
        Assert.Contains("96321", speakersLine);
    }

    /// <summary>
    ///     Test that <c>--direction input</c> lists only capture devices.
    /// </summary>
    [Fact]
    public void ListDevicesCommand_Run_DirectionInput_ListsOnlyCaptureDevices()
    {
        // Arrange
        var context = CreateContext(["--direction", "input"]);
        var captureProbe = new FakeAudioCaptureDeviceProbe([InputDevice]);
        var playbackProbe = new FakeAudioPlaybackDeviceProbe([OutputDevice]);

        // Act
        var output = RunCapturingOutput(context, captureProbe, playbackProbe);

        // Assert
        Assert.Contains("Fake Microphone", output);
        Assert.DoesNotContain("Fake Speakers", output);
    }

    /// <summary>
    ///     Test that <c>--direction output</c> lists only playback devices.
    /// </summary>
    [Fact]
    public void ListDevicesCommand_Run_DirectionOutput_ListsOnlyPlaybackDevices()
    {
        // Arrange
        var context = CreateContext(["--direction", "output"]);
        var captureProbe = new FakeAudioCaptureDeviceProbe([InputDevice]);
        var playbackProbe = new FakeAudioPlaybackDeviceProbe([OutputDevice]);

        // Act
        var output = RunCapturingOutput(context, captureProbe, playbackProbe);

        // Assert
        Assert.DoesNotContain("Fake Microphone", output);
        Assert.Contains("Fake Speakers", output);
    }

    /// <summary>
    ///     Test that no devices found prints a clean explanatory message instead of a bare table,
    ///     matching this library's graceful-degradation convention for unavailable audio.
    /// </summary>
    [Fact]
    public void ListDevicesCommand_Run_NoDevices_PrintsNoDevicesMessage()
    {
        // Arrange
        var context = CreateContext([]);
        var captureProbe = new FakeAudioCaptureDeviceProbe();
        var playbackProbe = new FakeAudioPlaybackDeviceProbe();

        // Act
        var output = RunCapturingOutput(context, captureProbe, playbackProbe);

        // Assert
        Assert.Contains("No devices found.", output);
    }

    /// <summary>
    ///     Test that an invalid <c>--direction</c> value throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void ListDevicesCommand_Run_InvalidDirectionValue_ThrowsArgumentException()
    {
        // Arrange
        var context = CreateContext(["--direction", "sideways"]);

        // Act
        void Act() => ListDevicesCommand.Run(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe());

        // Assert
        Assert.Throws<ArgumentException>(Act);
    }

    /// <summary>
    ///     Test that a <c>--direction</c> flag missing its value throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void ListDevicesCommand_Run_DirectionFlagMissingValue_ThrowsArgumentException()
    {
        // Arrange
        var context = CreateContext(["--direction"]);

        // Act
        void Act() => ListDevicesCommand.Run(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe());

        // Assert
        var exception = Assert.Throws<ArgumentException>(Act);
        Assert.Contains("--direction requires a value", exception.Message);
    }

    /// <summary>
    ///     Test that <c>--direction</c> followed by another option token is rejected rather than
    ///     swallowing the option as the direction value.
    /// </summary>
    [Fact]
    public void ListDevicesCommand_Run_DirectionFlagFollowedByOption_ThrowsArgumentException()
    {
        // Arrange
        var context = CreateContext(["--direction", "--direction", "input"]);

        // Act
        void Act() => ListDevicesCommand.Run(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe());

        // Assert
        var exception = Assert.Throws<ArgumentException>(Act);
        Assert.Contains("--direction requires a value", exception.Message);
    }

    /// <summary>
    ///     Test that an unsupported flag throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void ListDevicesCommand_Run_UnsupportedFlag_ThrowsArgumentException()
    {
        // Arrange
        var context = CreateContext(["--bogus"]);

        // Act
        void Act() => ListDevicesCommand.Run(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe());

        // Assert
        Assert.Throws<ArgumentException>(Act);
    }

    /// <summary>
    ///     Test that a <see langword="null"/> context is rejected.
    /// </summary>
    [Fact]
    public void ListDevicesCommand_Run_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var captureProbe = new FakeAudioCaptureDeviceProbe();
        var playbackProbe = new FakeAudioPlaybackDeviceProbe();

        // Act
        void Act() => ListDevicesCommand.Run(null!, captureProbe, playbackProbe);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    /// <summary>
    ///     Test that a <see langword="null"/> capture probe is rejected.
    /// </summary>
    [Fact]
    public void ListDevicesCommand_Run_NullCaptureProbe_ThrowsArgumentNullException()
    {
        // Arrange
        var context = CreateContext([]);

        // Act
        void Act() => ListDevicesCommand.Run(context, null!, new FakeAudioPlaybackDeviceProbe());

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    /// <summary>
    ///     Test that a <see langword="null"/> playback probe is rejected.
    /// </summary>
    [Fact]
    public void ListDevicesCommand_Run_NullPlaybackProbe_ThrowsArgumentNullException()
    {
        // Arrange
        var context = CreateContext([]);

        // Act
        void Act() => ListDevicesCommand.Run(context, new FakeAudioCaptureDeviceProbe(), null!);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    /// <summary>
    ///     Creates a <see cref="Context"/> resolved to the <c>list-devices</c> command with the
    ///     given raw command arguments.
    /// </summary>
    private static Context CreateContext(IEnumerable<string> commandArgs) =>
        Context.Create(["list-devices", .. commandArgs]);

    /// <summary>
    ///     Runs <see cref="ListDevicesCommand.Run(Context, IAudioCaptureDeviceProbe, IAudioPlaybackDeviceProbe)"/>
    ///     while capturing everything written to stdout.
    /// </summary>
    private static string RunCapturingOutput(Context context, IAudioCaptureDeviceProbe captureProbe, IAudioPlaybackDeviceProbe playbackProbe)
    {
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            ListDevicesCommand.Run(context, captureProbe, playbackProbe);
            return outWriter.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
