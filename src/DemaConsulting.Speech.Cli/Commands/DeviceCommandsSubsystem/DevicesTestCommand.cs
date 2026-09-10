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

// cspell:ignore NOSONAR
using System.Globalization;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Cli.Cli;

namespace DemaConsulting.Speech.Cli.Commands.DeviceCommandsSubsystem;

/// <summary>
///     Implements the <c>devices test</c> subcommand: sanity-checks a single audio device
///     without needing any speech model - playing a short synthesized tone for an output device,
///     or recording a short clip and reporting basic capture statistics for an input device.
/// </summary>
/// <remarks>
///     <para>
///     <c>devices</c> currently recognizes exactly one sub-action, <c>test</c>; the first
///     positional argument in <see cref="Context.CommandArgs"/> must be that literal token,
///     leaving room for the dispatch table's <c>devices</c> entry to grow additional sub-actions
///     later without changing its recognized top-level name.
///     </para>
///     <para>
///     When <c>--direction</c> is not given, this command defaults to testing the <b>output</b>
///     direction: playing a tone requires no operator consent beyond running the command,
///     whereas testing input necessarily records real audio input, which is a more
///     privacy-sensitive default to assume implicitly. An operator who wants the input path
///     tested passes <c>--direction input</c> explicitly.
///     </para>
///     <para>
///     This command inherently requires real audio hardware to be meaningful. Per this
///     library's honest-failure convention (see <see cref="UnavailableAudioPlaybackDevice"/>/
///     <see cref="UnavailableAudioCaptureDevice"/>), a resolved device that reports
///     <c>IsAvailable == false</c> is reported as a clean <see cref="InvalidOperationException"/>
///     rather than a stack trace.
///     </para>
/// </remarks>
internal static class DevicesTestCommand
{
    /// <summary>The fixed test-tone duration, in seconds, used by the output test.</summary>
    private const double ToneDurationSeconds = 2.0;

    /// <summary>The fixed test-tone frequency, in Hz, used by the output test.</summary>
    private const double ToneFrequencyHz = 440.0;

    /// <summary>The fixed capture-clip duration, in seconds, used by the input test.</summary>
    private const double CaptureDurationSeconds = 2.0;

    /// <summary>
    ///     Runs the <c>devices</c> subcommand (currently only the <c>test</c> sub-action) against
    ///     a real, composed <see cref="AudioDeviceFactory"/>.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the sub-action or an option/value is missing or unsupported.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the resolved device is not available.</exception>
    public static void Run(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var factory = new AudioDeviceFactory();
        Run(context, factory);
    }

    /// <summary>
    ///     Runs the <c>devices</c> subcommand against an injected <see cref="AudioDeviceFactory"/>,
    ///     letting a test supply one composed over fake probes (via
    ///     <see cref="AudioDeviceFactory(IAudioCaptureDeviceProbe?, IAudioPlaybackDeviceProbe?, DemaConsulting.Speech.Diagnostics.ISpeechDiagnostics?)"/>)
    ///     to exercise the device-not-found error path deterministically. The actual device
    ///     creation and audio I/O below that point still depend on the real native audio runtime
    ///     the factory was composed against, and are not exercised by this seam; see this
    ///     subsystem's verification document for why that path is manual/local verification only.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <param name="factory">The audio device factory to resolve devices from. Must not be null.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="context"/> or <paramref name="factory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the sub-action or an option/value is missing or unsupported.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the resolved device is not available.</exception>
    internal static void Run(Context context, AudioDeviceFactory factory)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(factory);

        var options = ParseArguments(context.CommandArgs);

        if (options.Direction == AudioDeviceDirection.Playback)
        {
            RunPlaybackTest(context, factory, options.DeviceName);
        }
        else
        {
            RunCaptureTest(context, factory, options.DeviceName);
        }
    }

    /// <summary>
    ///     Parses the <c>devices test [--device &lt;name&gt;] [--direction input|output]</c>
    ///     arguments.
    /// </summary>
    /// <param name="args">The raw command arguments, starting with the sub-action token.</param>
    /// <returns>The parsed device name and direction.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when the sub-action is missing or is not <c>test</c>, or when an option/value
    ///     is missing or unsupported.
    /// </exception>
    internal static DevicesTestOptions ParseArguments(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || !string.Equals(args[0], "test", StringComparison.Ordinal))
        {
            throw new ArgumentException("devices requires a 'test' sub-action, e.g. 'devices test'.", nameof(args));
        }

        string? deviceName = null;
        var direction = AudioDeviceDirection.Playback;

        var index = 1;
        while (index < args.Count)
        {
            var arg = args[index++];
            switch (arg)
            {
                case "--device":
                    deviceName = RequireValue(args, ref index, "--device");
                    break;

                case "--direction":
                    var value = RequireValue(args, ref index, "--direction");
                    direction = value switch
                    {
                        "input" => AudioDeviceDirection.Capture,
                        "output" => AudioDeviceDirection.Playback,
                        _ => throw new ArgumentException($"--direction must be 'input' or 'output', not '{value}'.", nameof(args))
                    };
                    break;

                default:
                    throw new ArgumentException($"Unsupported argument '{arg}' for 'devices test'.", nameof(args));
            }
        }

        return new DevicesTestOptions(deviceName, direction);
    }

    /// <summary>
    ///     Gets the value following a flag, throwing a clean <see cref="ArgumentException"/> when
    ///     none was supplied.
    /// </summary>
    private static string RequireValue(IReadOnlyList<string> args, ref int index, string flag)
    {
        if (index >= args.Count)
        {
            throw new ArgumentException($"{flag} requires a value.", nameof(args));
        }

        return args[index++];
    }

    /// <summary>
    ///     Validates that a requested device name (if any) matches a device currently enumerated
    ///     for the given direction, throwing a clean error naming the device rather than letting
    ///     device creation fail obscurely.
    /// </summary>
    /// <param name="knownDevices">The devices currently enumerated for the requested direction.</param>
    /// <param name="deviceName">The requested device name, or <see langword="null"/> for the system default.</param>
    /// <param name="directionLabel">A human-readable direction label used in the error message.</param>
    /// <returns>
    ///     An <see cref="AudioDeviceSelection"/> for <paramref name="deviceName"/> when it is
    ///     not <see langword="null"/>, otherwise <see langword="null"/> (system default).
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="deviceName"/> is not <see langword="null"/> and does not
    ///     match any device in <paramref name="knownDevices"/>.
    /// </exception>
    internal static AudioDeviceSelection? ResolveDeviceSelectionOrThrow(
        IReadOnlyList<AudioDeviceDescription> knownDevices,
        string? deviceName,
        string directionLabel)
    {
        ArgumentNullException.ThrowIfNull(knownDevices);

        if (deviceName is null)
        {
            return null;
        }

        var matches = knownDevices.Any(device => string.Equals(device.Name, deviceName, StringComparison.Ordinal));
        if (!matches)
        {
            throw new ArgumentException(
                $"Unknown {directionLabel} device '{deviceName}'. Use 'list-devices' to see available devices.",
                nameof(deviceName));
        }

        return new AudioDeviceSelection(deviceName);
    }

    /// <summary>
    ///     Generates a mono or multi-channel test tone as normalized 32-bit floating point
    ///     samples, ready to pass to <see cref="IAudioPlaybackDevice.Write"/>.
    /// </summary>
    /// <param name="sampleRate">The sample rate, in Hz, to generate the tone at. Must be greater than zero.</param>
    /// <param name="channelCount">The number of interleaved channels to generate. Must be greater than zero.</param>
    /// <param name="durationSeconds">The tone duration, in seconds. Must be greater than zero.</param>
    /// <param name="frequencyHz">The tone frequency, in Hz. Must be greater than zero.</param>
    /// <returns>
    ///     An array of <c>sampleRate * durationSeconds * channelCount</c> interleaved samples in
    ///     the range <c>[-1.0, 1.0]</c>, containing one sine wave cycle set repeated for the
    ///     requested duration, identical across every channel.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="sampleRate"/>, <paramref name="channelCount"/>,
    ///     <paramref name="durationSeconds"/>, or <paramref name="frequencyHz"/> is less than or
    ///     equal to zero.
    /// </exception>
    internal static float[] GenerateToneSamples(int sampleRate, int channelCount, double durationSeconds, double frequencyHz)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(channelCount, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(durationSeconds, 0.0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frequencyHz, 0.0);

        var frameCount = (int)(sampleRate * durationSeconds);
        var samples = new float[frameCount * channelCount];

        // A gentle fixed amplitude, well below full scale, so a test tone is audible but never
        // uncomfortably loud regardless of the resolved device's own volume level.
        const float amplitude = 0.2f;

        for (var frame = 0; frame < frameCount; frame++)
        {
            var value = amplitude * (float)Math.Sin(2.0 * Math.PI * frequencyHz * frame / sampleRate);
            for (var channel = 0; channel < channelCount; channel++)
            {
                samples[(frame * channelCount) + channel] = value;
            }
        }

        return samples;
    }

    /// <summary>
    ///     Plays a short synthesized test tone on the resolved playback device.
    /// </summary>
    private static void RunPlaybackTest(Context context, AudioDeviceFactory factory, string? deviceName)
    {
        var knownDevices = factory.PlaybackProbe.Enumerate();
        var selection = ResolveDeviceSelectionOrThrow(knownDevices, deviceName, "playback");

        var device = factory.CreatePlaybackDevice(selection);
        if (!device.IsAvailable)
        {
            throw new InvalidOperationException(
                "No audio playback device is available on this machine; cannot run 'devices test'.");
        }

        var samples = GenerateToneSamples(device.SampleRate, device.ChannelCount, ToneDurationSeconds, ToneFrequencyHz);

        device.Start();
        try
        {
            device.Write(samples);

            // Write is fire-and-forget: poll PendingSampleCount until the hardware has actually
            // consumed the queued tone, per IAudioPlaybackDevice's own documented contract.
            while (device.PendingSampleCount > 0)
            {
                Thread.Sleep(50);
            }
        }
        finally
        {
            device.Stop();
        }

        context.WriteLine(
            $"Played a {ToneDurationSeconds:0.#}-second {ToneFrequencyHz:0} Hz test tone " +
            $"(sample rate {device.SampleRate.ToString(CultureInfo.InvariantCulture)} Hz, " +
            $"{device.ChannelCount.ToString(CultureInfo.InvariantCulture)} channel(s)).");
    }

    /// <summary>
    ///     Records a short clip from the resolved capture device and reports basic statistics
    ///     proving capture worked, without saving or playing back the recording.
    /// </summary>
    private static void RunCaptureTest(Context context, AudioDeviceFactory factory, string? deviceName)
    {
        var knownDevices = factory.CaptureProbe.Enumerate();
        var selection = ResolveDeviceSelectionOrThrow(knownDevices, deviceName, "capture");

        var device = factory.CreateCaptureDevice(selection);
        if (!device.IsAvailable)
        {
            throw new InvalidOperationException(
                "No audio capture device is available on this machine; cannot run 'devices test'.");
        }

        var samples = new List<float>();
        void OnFrameCaptured(object? sender, AudioCaptureFrameEventArgs e) => samples.AddRange(e.Samples);

        device.FrameCaptured += OnFrameCaptured;
        try
        {
            device.Start();
            Thread.Sleep(TimeSpan.FromSeconds(CaptureDurationSeconds));
            device.Stop();
        }
        finally
        {
            device.FrameCaptured -= OnFrameCaptured;
        }

        // NOSONAR (S2583): SonarQube's flow analysis cannot see that OnFrameCaptured mutates
        // `samples` via the FrameCaptured event subscription above, so it believes the list can
        // never be non-empty and reports the "samples.Count == 0" branch as always true. At
        // runtime the capture device populates `samples` through the event before this line is
        // reached, so both branches are genuinely reachable; the guard is required to avoid
        // MaxMagnitude() throwing on an empty span when the device delivers no frames in time.
        // CollectionsMarshal.AsSpan exposes the list's backing array with no copy, and
        // TensorPrimitives.MaxMagnitude finds the largest-magnitude element in one vectorized
        // pass instead of LINQ's Max(Math.Abs) invoking a delegate per element.
        var sampleSpan = CollectionsMarshal.AsSpan(samples);
        var peakAmplitude = sampleSpan.IsEmpty ? 0.0f : Math.Abs(TensorPrimitives.MaxMagnitude(sampleSpan)); // NOSONAR

        context.WriteLine(
            $"Recorded {CaptureDurationSeconds:0.#} second(s) from the resolved capture device " +
            $"(sample rate {device.SampleRate.ToString(CultureInfo.InvariantCulture)} Hz, " +
            $"{device.ChannelCount.ToString(CultureInfo.InvariantCulture)} channel(s)).");
        context.WriteLine($"Samples captured: {samples.Count.ToString(CultureInfo.InvariantCulture)}");
        context.WriteLine($"Peak amplitude: {peakAmplitude.ToString("0.000", CultureInfo.InvariantCulture)}");
    }

    /// <summary>The parsed <c>devices test</c> flags.</summary>
    /// <param name="DeviceName">The requested device name, or <see langword="null"/> for the system default.</param>
    /// <param name="Direction">The requested test direction; defaults to <see cref="AudioDeviceDirection.Playback"/>.</param>
    internal sealed record DevicesTestOptions(string? DeviceName, AudioDeviceDirection Direction);
}
