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

using System.Globalization;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Cli.Cli;
using DemaConsulting.Speech.Cli.Commands.DeviceCommandsSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.DeviceCommandsSubsystem;

/// <summary>
///     Unit tests for <see cref="DevicesTestCommand"/>'s hardware-independent logic: argument
///     parsing, device-not-found handling, tone-generation math, and the tone playback and capture
///     recording steps against fake devices. Real device creation depends on the native audio runtime
///     and is not exercised here; see <c>docs/verification/speech-cli/device-commands-subsystem.md</c>
///     for why that step is manual/local verification only.
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
        // Arrange
        string[] args = ["test"];

        // Act
        var options = DevicesTestCommand.ParseArguments(args);

        // Assert
        Assert.Null(options.DeviceName);
        Assert.Equal(AudioDeviceDirection.Playback, options.Direction);
    }

    /// <summary>
    ///     Test that <c>--direction input</c> parses to the capture direction.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_DirectionInput_ParsesToCaptureDirection()
    {
        // Arrange
        string[] args = ["test", "--direction", "input"];

        // Act
        var options = DevicesTestCommand.ParseArguments(args);

        // Assert
        Assert.Equal(AudioDeviceDirection.Capture, options.Direction);
    }

    /// <summary>
    ///     Test that <c>--direction output</c> parses to the playback direction.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_DirectionOutput_ParsesToPlaybackDirection()
    {
        // Arrange
        string[] args = ["test", "--direction", "output"];

        // Act
        var options = DevicesTestCommand.ParseArguments(args);

        // Assert
        Assert.Equal(AudioDeviceDirection.Playback, options.Direction);
    }

    /// <summary>
    ///     Test that <c>--device &lt;name&gt;</c> parses the requested device name.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_DeviceFlag_ParsesDeviceName()
    {
        // Arrange
        string[] args = ["test", "--device", "My Speakers"];

        // Act
        var options = DevicesTestCommand.ParseArguments(args);

        // Assert
        Assert.Equal("My Speakers", options.DeviceName);
    }

    /// <summary>
    ///     Test that a missing sub-action throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_MissingSubAction_ThrowsArgumentException()
    {
        // Arrange
        string[] args = [];

        // Act
        void Act() => DevicesTestCommand.ParseArguments(args);

        // Assert
        Assert.Throws<ArgumentException>(Act);
    }

    /// <summary>
    ///     Test that a sub-action other than <c>test</c> throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_UnknownSubAction_ThrowsArgumentException()
    {
        // Arrange
        string[] args = ["bogus"];

        // Act
        void Act() => DevicesTestCommand.ParseArguments(args);

        // Assert
        Assert.Throws<ArgumentException>(Act);
    }

    /// <summary>
    ///     Test that an invalid <c>--direction</c> value throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_InvalidDirectionValue_ThrowsArgumentException()
    {
        // Arrange
        string[] args = ["test", "--direction", "sideways"];

        // Act
        void Act() => DevicesTestCommand.ParseArguments(args);

        // Assert
        Assert.Throws<ArgumentException>(Act);
    }

    /// <summary>
    ///     Test that an unsupported flag throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_UnsupportedFlag_ThrowsArgumentException()
    {
        // Arrange
        string[] args = ["test", "--bogus"];

        // Act
        void Act() => DevicesTestCommand.ParseArguments(args);

        // Assert
        Assert.Throws<ArgumentException>(Act);
    }

    /// <summary>
    ///     Test that a <c>--device</c> flag missing its value throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_DeviceFlagMissingValue_ThrowsArgumentException()
    {
        // Arrange
        string[] args = ["test", "--device"];

        // Act
        void Act() => DevicesTestCommand.ParseArguments(args);

        // Assert
        var exception = Assert.Throws<ArgumentException>(Act);
        Assert.Contains("--device requires a value", exception.Message);
    }

    /// <summary>
    ///     Test that a <c>--direction</c> flag missing its value throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_DirectionFlagMissingValue_ThrowsArgumentException()
    {
        // Arrange
        string[] args = ["test", "--direction"];

        // Act
        void Act() => DevicesTestCommand.ParseArguments(args);

        // Assert
        var exception = Assert.Throws<ArgumentException>(Act);
        Assert.Contains("--direction requires a value", exception.Message);
    }

    /// <summary>
    ///     Test that <c>--device</c> followed by another recognized option is rejected rather than
    ///     swallowing the option token as the device name.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_DeviceFlagFollowedByOption_ThrowsArgumentException()
    {
        // Arrange
        string[] args = ["test", "--device", "--direction", "input"];

        // Act
        void Act() => DevicesTestCommand.ParseArguments(args);

        // Assert
        var exception = Assert.Throws<ArgumentException>(Act);
        Assert.Contains("--device requires a value", exception.Message);
    }

    /// <summary>
    ///     Test that <c>--direction</c> followed by another recognized option is rejected rather than
    ///     swallowing the option token as the direction.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ParseArguments_DirectionFlagFollowedByOption_ThrowsArgumentException()
    {
        // Arrange
        string[] args = ["test", "--direction", "--device", "x"];

        // Act
        void Act() => DevicesTestCommand.ParseArguments(args);

        // Assert
        var exception = Assert.Throws<ArgumentException>(Act);
        Assert.Contains("--direction requires a value", exception.Message);
    }

    /// <summary>
    ///     Test that a <see langword="null"/> device name resolves to the system default
    ///     (a <see langword="null"/> selection) without needing any known devices.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ResolveDeviceSelectionOrThrow_NullDeviceName_ReturnsNull()
    {
        // Arrange
        IReadOnlyList<AudioDeviceDescription> known = [];

        // Act
        var selection = DevicesTestCommand.ResolveDeviceSelectionOrThrow(known, null, "playback");

        // Assert
        Assert.Null(selection);
    }

    /// <summary>
    ///     Test that a device name matching a known device resolves to a selection for it.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ResolveDeviceSelectionOrThrow_KnownDeviceName_ReturnsSelection()
    {
        // Arrange
        IReadOnlyList<AudioDeviceDescription> known = [OutputDevice];

        // Act
        var selection = DevicesTestCommand.ResolveDeviceSelectionOrThrow(known, OutputDevice.Name, "playback");

        // Assert
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
        // Arrange
        IReadOnlyList<AudioDeviceDescription> known = [InputDevice];

        // Act
        void Act() => DevicesTestCommand.ResolveDeviceSelectionOrThrow(known, "does-not-exist", "capture");

        // Assert
        var exception = Assert.Throws<ArgumentException>(Act);
        Assert.Contains("does-not-exist", exception.Message);
        Assert.Contains("list-devices", exception.Message);
    }

    /// <summary>
    ///     Test that a <see langword="null"/> known-devices list is rejected.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_ResolveDeviceSelectionOrThrow_NullKnownDevices_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<AudioDeviceDescription>? known = null;

        // Act
        void Act() => DevicesTestCommand.ResolveDeviceSelectionOrThrow(known!, "any", "playback");

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    /// <summary>
    ///     Test that the generated tone has the expected sample count for the requested duration,
    ///     sample rate, and channel count.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_GenerateToneSamples_ReturnsExpectedSampleCount()
    {
        // Arrange
        const int sampleRate = 1000;

        // Act
        var samples = DevicesTestCommand.GenerateToneSamples(sampleRate, channelCount: 2, durationSeconds: 1.0, frequencyHz: 100.0);

        // Assert
        Assert.Equal(2000, samples.Length);
    }

    /// <summary>
    ///     Test that every generated sample stays within the normalized <c>[-1.0, 1.0]</c> range.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_GenerateToneSamples_AllSamplesWithinNormalizedRange()
    {
        // Arrange
        const int sampleRate = 44100;

        // Act
        var samples = DevicesTestCommand.GenerateToneSamples(sampleRate, channelCount: 1, durationSeconds: 0.5, frequencyHz: 440.0);

        // Assert
        Assert.All(samples, sample => Assert.InRange(sample, -1.0f, 1.0f));
    }

    /// <summary>
    ///     Test that every channel carries an identical value for a given frame, since the tone
    ///     is generated identically across channels.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_GenerateToneSamples_IdenticalAcrossChannels()
    {
        // Arrange
        const int sampleRate = 8000;

        // Act
        var samples = DevicesTestCommand.GenerateToneSamples(sampleRate, channelCount: 2, durationSeconds: 0.1, frequencyHz: 440.0);

        // Assert
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
        // Arrange
        const int sampleRate = 0;

        // Act
        void Act() => DevicesTestCommand.GenerateToneSamples(sampleRate, channelCount: 1, durationSeconds: 1.0, frequencyHz: 440.0);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(Act);
    }

    /// <summary>
    ///     Test that a non-positive channel count, duration or frequency is rejected.
    /// </summary>
    /// <param name="channelCount">The channel count to use.</param>
    /// <param name="durationSeconds">The duration to use.</param>
    /// <param name="frequencyHz">The frequency to use.</param>
    [Theory]
    [InlineData(0, 1.0, 440.0)]
    [InlineData(-1, 1.0, 440.0)]
    [InlineData(1, 0.0, 440.0)]
    [InlineData(1, -1.0, 440.0)]
    [InlineData(1, 1.0, 0.0)]
    [InlineData(1, 1.0, -440.0)]
    public void DevicesTestCommand_GenerateToneSamples_NonPositiveOtherArguments_ThrowsArgumentOutOfRangeException(
        int channelCount, double durationSeconds, double frequencyHz)
    {
        // Arrange: valid sample rate with one non-positive argument
        const int sampleRate = 48000;

        // Act
        void Act() => DevicesTestCommand.GenerateToneSamples(sampleRate, channelCount, durationSeconds, frequencyHz);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(Act);
    }

    /// <summary>
    ///     Test that an unknown requested device throws a clean <see cref="ArgumentException"/>
    ///     before any device is created, using a real <see cref="AudioDeviceFactory"/> composed
    ///     over fake probes via its public constructor.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_Run_UnknownPlaybackDevice_ThrowsArgumentException()
    {
        // Arrange
        var context = Context.Create(["devices", "test", "--device", "does-not-exist", "--direction", "output"]);
        var factory = new AudioDeviceFactory(new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe([OutputDevice]));

        // Act
        void Act() => DevicesTestCommand.Run(context, factory);

        // Assert
        var exception = Assert.Throws<ArgumentException>(Act);
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>
    ///     Test that an unknown requested capture device throws a clean
    ///     <see cref="ArgumentException"/> before any device is created.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_Run_UnknownCaptureDevice_ThrowsArgumentException()
    {
        // Arrange
        var context = Context.Create(["devices", "test", "--device", "does-not-exist", "--direction", "input"]);
        var factory = new AudioDeviceFactory(new FakeAudioCaptureDeviceProbe([InputDevice]), new FakeAudioPlaybackDeviceProbe());

        // Act
        void Act() => DevicesTestCommand.Run(context, factory);

        // Assert
        var exception = Assert.Throws<ArgumentException>(Act);
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>
    ///     Test that a <see langword="null"/> context is rejected.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_Run_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new AudioDeviceFactory();

        // Act
        void Act() => DevicesTestCommand.Run(null!, factory);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    /// <summary>
    ///     Test that a <see langword="null"/> factory is rejected.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_Run_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var context = Context.Create(["devices", "test"]);

        // Act
        void Act() => DevicesTestCommand.Run(context, null!);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    /// <summary>
    ///     Test that the output test writes the full two-second tone to the device (one frame per sample per channel),
    ///     waits for it to drain, and reports the played tone.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_PlayTestTone_AvailableDevice_WritesToneAndReportsIt()
    {
        // Arrange
        var context = Context.Create(["devices", "test"]);
        var device = new RecordingPlaybackDevice();

        // Act
        var output = CaptureOutput(() => DevicesTestCommand.PlayTestTone(context, device));

        // Assert
        Assert.True(device.Started);
        Assert.True(device.Stopped);
        Assert.Equal(48000 * 2 * 2, device.Written.Count);
        Assert.Contains("440 Hz", output);
        Assert.Contains("48000 Hz", output);
    }

    /// <summary>
    ///     Test that a playback device whose queue never drains is reported as a failure within the bounded
    ///     wait instead of hanging, and is still stopped.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_PlayTestTone_DeviceNeverDrains_ThrowsInvalidOperationException()
    {
        // Arrange
        var context = Context.Create(["devices", "test"]);
        var device = new RecordingPlaybackDevice { PendingSampleCount = 1 };

        // Act
        // The drain limit is injected, so the stalled-device path completes immediately.
        void Act() => DevicesTestCommand.PlayTestTone(context, device, TimeSpan.FromMilliseconds(1));

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.True(device.Stopped);
        Assert.Contains("did not finish", exception.Message);
    }

    /// <summary>
    ///     Test that the time spent starting the playback device counts toward the total time limit: a
    ///     device whose <c>Start</c> alone exceeds the limit is reported as not finishing even though its
    ///     queue then drains immediately. Without the total bound (a drain-only clock), this would succeed.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_PlayTestTone_SlowStartExceedsTotalLimit_ThrowsInvalidOperationException()
    {
        // Arrange
        var context = Context.Create(["devices", "test"]);
        var device = new RecordingPlaybackDevice
        {
            StartDelay = TimeSpan.FromMilliseconds(250),
            PendingSampleCount = 1,
            DrainAfterFirstRead = true
        };

        // Act
        void Act() => DevicesTestCommand.PlayTestTone(context, device, TimeSpan.FromMilliseconds(50));

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("did not finish", exception.Message);
        Assert.True(device.Stopped);
    }

    /// <summary>
    ///     Test that a <c>Write</c> that blocks past the total limit is reported as a failure instead of
    ///     hanging the command, and the device is still stopped.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_PlayTestTone_WriteBlocksPastLimit_ThrowsInvalidOperationException()
    {
        // Arrange: a device whose Write blocks until released
        var context = Context.Create(["devices", "test"]);
        using var gate = new ManualResetEventSlim(false);
        var device = new RecordingPlaybackDevice { WriteGate = gate };

        try
        {
            // Act: play with a short total limit
            void Act() => DevicesTestCommand.PlayTestTone(context, device, TimeSpan.FromMilliseconds(100));

            // Assert: the timeout error is raised and the device was stopped
            var exception = Assert.Throws<InvalidOperationException>(Act);
            Assert.Contains("did not finish", exception.Message);
            Assert.True(device.Stopped);
        }
        finally
        {
            gate.Set();
        }
    }

    /// <summary>
    ///     Test that a slow <c>Write</c> followed by an empty queue is still a failure when it overruns the
    ///     total limit, so success is never reported after the deadline.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_PlayTestTone_SlowWriteWithEmptyQueue_ThrowsInvalidOperationException()
    {
        // Arrange: a Write that sleeps past the limit and a queue that is already empty
        var context = Context.Create(["devices", "test"]);
        var device = new RecordingPlaybackDevice { WriteDelay = TimeSpan.FromMilliseconds(300) };

        // Act
        void Act() => DevicesTestCommand.PlayTestTone(context, device, TimeSpan.FromMilliseconds(50));

        // Assert: no success is reported
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("did not finish", exception.Message);
        Assert.True(device.Stopped);
    }

    /// <summary>
    ///     Test that the playback device is stopped even when writing the tone throws.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_PlayTestTone_WriteThrows_StillStopsDevice()
    {
        // Arrange
        var context = Context.Create(["devices", "test"]);
        var device = new RecordingPlaybackDevice { WriteException = new InvalidOperationException("write failed") };

        // Act
        void Act() => DevicesTestCommand.PlayTestTone(context, device);

        // Assert
        Assert.Throws<InvalidOperationException>(Act);
        Assert.True(device.Stopped);
    }

    /// <summary>
    ///     Test that the playback device is stopped even when starting it throws partway, so a
    ///     partially started device is never left running.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_PlayTestTone_StartThrows_StillStopsDevice()
    {
        // Arrange
        var context = Context.Create(["devices", "test"]);
        var device = new RecordingPlaybackDevice { StartException = new InvalidOperationException("start failed") };

        // Act
        void Act() => DevicesTestCommand.PlayTestTone(context, device);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Equal("start failed", exception.Message);
        Assert.True(device.Stopped);
    }

    /// <summary>
    ///     Test that the reported durations use the invariant culture (a decimal point) regardless of the
    ///     current culture.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_RecordTestClip_CommaDecimalCulture_ReportsInvariantDuration()
    {
        // Arrange
        var context = Context.Create(["devices", "test", "--direction", "input"]);
        var device = new FramingCaptureDevice([0.1f]);
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            // Act
            var output = CaptureOutput(() => DevicesTestCommand.RecordTestClip(context, device, TimeSpan.FromMilliseconds(1500)));

            // Assert
            Assert.Contains("Recorded 1.5 second(s)", output);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    /// <summary>
    ///     Test that the input test reports the number of samples recorded and the peak amplitude.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_RecordTestClip_DeviceDeliversFrames_ReportsSampleCountAndPeak()
    {
        // Arrange
        var context = Context.Create(["devices", "test", "--direction", "input"]);
        var device = new FramingCaptureDevice([0.1f, -0.5f, 0.25f]);

        // Act
        var output = CaptureOutput(() => DevicesTestCommand.RecordTestClip(context, device, TimeSpan.FromMilliseconds(10)));

        // Assert
        Assert.True(device.Started);
        Assert.True(device.Stopped);
        Assert.Contains("Samples captured: 3", output);
        Assert.Contains("Peak amplitude: 0.500", output);
    }

    /// <summary>
    ///     Test that an input test during which the device delivers no samples fails instead of reporting success.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_RecordTestClip_DeviceDeliversNoFrames_ThrowsInvalidOperationException()
    {
        // Arrange
        var context = Context.Create(["devices", "test", "--direction", "input"]);
        var device = new FramingCaptureDevice([]);

        // Act
        void Act() => DevicesTestCommand.RecordTestClip(context, device, TimeSpan.FromMilliseconds(10));

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.True(device.Stopped);
        Assert.Contains("no audio samples", exception.Message);
    }

    /// <summary>
    ///     Test that a capture device whose <c>Start</c> throws is still stopped and unsubscribed, so a
    ///     partially started device is never left running.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_RecordTestClip_StartThrows_StopsDevice()
    {
        // Arrange
        var context = Context.Create(["devices", "test", "--direction", "input"]);
        var device = new FramingCaptureDevice([0.1f]) { ThrowOnStart = true };

        // Act
        void Act() => DevicesTestCommand.RecordTestClip(context, device, TimeSpan.FromMilliseconds(10));

        // Assert
        Assert.Throws<InvalidOperationException>(Act);
        Assert.True(device.Stopped);
    }

    /// <summary>
    ///     Test that an unavailable playback device is rejected with a clean error naming the playback device.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_RequireAvailable_UnavailablePlaybackDevice_ThrowsInvalidOperationException()
    {
        // Arrange
        var device = new RecordingPlaybackDevice { Available = false };

        // Act
        void Act() => DevicesTestCommand.RequireAvailable(device);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("playback", exception.Message);
    }

    /// <summary>
    ///     Test that an unavailable capture device is rejected with a clean error naming the capture device.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_RequireAvailable_UnavailableCaptureDevice_ThrowsInvalidOperationException()
    {
        // Arrange
        var device = new FramingCaptureDevice([]) { Available = false };

        // Act
        void Act() => DevicesTestCommand.RequireAvailable(device);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("capture", exception.Message);
    }

    /// <summary>
    ///     Test that available playback and capture devices are returned unchanged.
    /// </summary>
    [Fact]
    public void DevicesTestCommand_RequireAvailable_AvailableDevices_ReturnsSameDevice()
    {
        // Arrange
        var playback = new RecordingPlaybackDevice();
        var capture = new FramingCaptureDevice([]);

        // Act
        var playbackResult = DevicesTestCommand.RequireAvailable(playback);
        var captureResult = DevicesTestCommand.RequireAvailable(capture);

        // Assert
        Assert.Same(playback, playbackResult);
        Assert.Same(capture, captureResult);
    }

    /// <summary>Runs an action while capturing everything written to stdout.</summary>
    private static string CaptureOutput(Action action)
    {
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            action();
            return outWriter.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>A playback device that records what was written to it.</summary>
    private sealed class RecordingPlaybackDevice : IAudioPlaybackDevice
    {
        /// <summary>Tracks whether the pending count has been read once already.</summary>
        private bool _pendingRead;

        /// <summary>Gets the samples written so far.</summary>
        public List<float> Written { get; } = [];

        /// <summary>Gets a value indicating whether <see cref="Start"/> was called.</summary>
        public bool Started { get; private set; }

        /// <summary>Gets a value indicating whether <see cref="Stop"/> was called.</summary>
        public bool Stopped { get; private set; }

        /// <summary>Gets or sets a value indicating whether the device reports itself available.</summary>
        public bool Available { get; set; } = true;

        /// <summary>Gets or sets how long <see cref="Start"/> blocks before returning.</summary>
        public TimeSpan StartDelay { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether <see cref="PendingSampleCount"/> reports zero after
        ///     the first read, simulating a queue that drains immediately.
        /// </summary>
        public bool DrainAfterFirstRead { get; set; }

        /// <summary>Gets or sets an exception thrown by <see cref="Start"/> when not <see langword="null"/>.</summary>
        public Exception? StartException { get; set; }

        /// <inheritdoc/>
        public bool IsAvailable => Available;

        /// <inheritdoc/>
        public int ChannelCount => 2;

        /// <inheritdoc/>
        public int SampleRate => 48000;

        /// <summary>Gets or sets the pending sample count; see <see cref="DrainAfterFirstRead"/>.</summary>
        public long PendingSampleCount
        {
            get
            {
                if (DrainAfterFirstRead && _pendingRead)
                {
                    return 0;
                }

                _pendingRead = true;
                return field;
            }

            set;
        }

        /// <summary>Gets or sets an exception thrown by <see cref="Write"/> when not <see langword="null"/>.</summary>
        public Exception? WriteException { get; set; }

        /// <summary>Gets or sets how long <see cref="Write"/> blocks before returning.</summary>
        public TimeSpan WriteDelay { get; set; }

        /// <summary>
        ///     Gets or sets a gate that <see cref="Write"/> waits on (up to 30 seconds) before returning,
        ///     simulating a write that blocks until released; <see langword="null"/> means no gate.
        /// </summary>
        public ManualResetEventSlim? WriteGate { get; set; }

        /// <inheritdoc/>
        public void Start()
        {
            if (StartDelay > TimeSpan.Zero)
            {
                Thread.Sleep(StartDelay);
            }

            if (StartException != null)
            {
                throw StartException;
            }

            Started = true;
        }

        /// <inheritdoc/>
        public void Stop() => Stopped = true;

        /// <inheritdoc/>
        public void Write(IReadOnlyList<float> samples)
        {
            if (WriteDelay > TimeSpan.Zero)
            {
                Thread.Sleep(WriteDelay);
            }

            WriteGate?.Wait(TimeSpan.FromSeconds(30));

            if (WriteException != null)
            {
                throw WriteException;
            }

            Written.AddRange(samples);
        }
    }

    /// <summary>A capture device that raises one frame with the configured samples when started (none when empty).</summary>
    private sealed class FramingCaptureDevice(float[] samples) : IAudioCaptureDevice
    {
        /// <summary>Gets a value indicating whether <see cref="Start"/> was called.</summary>
        public bool Started { get; private set; }

        /// <summary>Gets a value indicating whether <see cref="Stop"/> was called.</summary>
        public bool Stopped { get; private set; }

        /// <summary>Gets or sets a value indicating whether the device reports itself available.</summary>
        public bool Available { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether <see cref="Start"/> throws after being marked started.</summary>
        public bool ThrowOnStart { get; set; }

        /// <inheritdoc/>
        public bool IsAvailable => Available;

        /// <inheritdoc/>
        public int ChannelCount => 1;

        /// <inheritdoc/>
        public int SampleRate => 16000;

        /// <inheritdoc/>
        public event EventHandler<AudioCaptureFrameEventArgs>? FrameCaptured;

        /// <inheritdoc/>
        public void Start()
        {
            Started = true;
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("Start failed after partially starting.");
            }

            if (samples.Length > 0)
            {
                FrameCaptured?.Invoke(this, new AudioCaptureFrameEventArgs(samples));
            }
        }

        /// <inheritdoc/>
        public void Stop() => Stopped = true;
    }
}
