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

namespace DemaConsulting.Speech.Cli.Commands.DeviceCommandsSubsystem;

/// <summary>
///     Implements the <c>list-devices</c> subcommand: enumerates the audio capture (input) and
///     playback (output) devices currently available on the host machine, optionally filtered to
///     one direction, as an aligned table.
/// </summary>
/// <remarks>
///     No CLI-owned seam is introduced for this command: <see cref="IAudioCaptureDeviceProbe"/>
///     and <see cref="IAudioPlaybackDeviceProbe"/> are already public, directly fakeable
///     interfaces, so wrapping them again (as <c>ICliModelCatalog</c> wraps the sealed
///     <c>SpeechModelCatalog</c>) would add an unnecessary extra layer for no testability
///     benefit.
/// </remarks>
internal static class ListDevicesCommand
{
    /// <summary>
    ///     Runs the <c>list-devices</c> subcommand against a real, composed
    ///     <see cref="AudioDeviceFactory"/>.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when an unsupported flag or flag value is given.</exception>
    public static void Run(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var factory = new AudioDeviceFactory();
        Run(context, factory.CaptureProbe, factory.PlaybackProbe);
    }

    /// <summary>
    ///     Runs the <c>list-devices</c> subcommand against injected device probes, for unit
    ///     testing without real audio hardware or a real <see cref="AudioDeviceFactory"/>.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <param name="captureProbe">The capture device probe to enumerate. Must not be null.</param>
    /// <param name="playbackProbe">The playback device probe to enumerate. Must not be null.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="context"/>, <paramref name="captureProbe"/>, or
    ///     <paramref name="playbackProbe"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when an unsupported flag or flag value is given.</exception>
    internal static void Run(Context context, IAudioCaptureDeviceProbe captureProbe, IAudioPlaybackDeviceProbe playbackProbe)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(captureProbe);
        ArgumentNullException.ThrowIfNull(playbackProbe);

        var direction = ParseArguments(context.CommandArgs);

        var devices = new List<AudioDeviceDescription>();
        if (direction is null or AudioDeviceDirection.Capture)
        {
            devices.AddRange(captureProbe.Enumerate());
        }

        if (direction is null or AudioDeviceDirection.Playback)
        {
            devices.AddRange(playbackProbe.Enumerate());
        }

        WriteTable(context, devices);
    }

    /// <summary>
    ///     Parses <c>list-devices</c>' own flag: <c>--direction input|output</c>.
    /// </summary>
    /// <param name="args">The raw command arguments.</param>
    /// <returns>
    ///     <see cref="AudioDeviceDirection.Capture"/> for <c>--direction input</c>,
    ///     <see cref="AudioDeviceDirection.Playback"/> for <c>--direction output</c>, or
    ///     <see langword="null"/> when <c>--direction</c> was not given, meaning both directions
    ///     are listed.
    /// </returns>
    private static AudioDeviceDirection? ParseArguments(IReadOnlyList<string> args)
    {
        AudioDeviceDirection? direction = null;

        var index = 0;
        while (index < args.Count)
        {
            var arg = args[index++];
            switch (arg)
            {
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
                    throw new ArgumentException($"Unsupported argument '{arg}' for 'list-devices'.", nameof(args));
            }
        }

        return direction;
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
    ///     Writes the given devices as an aligned, human-readable table, or an explanatory
    ///     message when none were found.
    /// </summary>
    /// <remarks>
    ///     Per this library's graceful-degradation convention for audio devices (see
    ///     <see cref="UnavailableAudioCaptureDeviceProbe"/>/<see cref="UnavailableAudioPlaybackDeviceProbe"/>),
    ///     an empty enumeration - whether because no native audio backend is available on this
    ///     platform, or because a device direction genuinely has no devices - is reported as a
    ///     plain "no devices" message, never as an error.
    /// </remarks>
    private static void WriteTable(Context context, List<AudioDeviceDescription> devices)
    {
        if (devices.Count == 0)
        {
            context.WriteLine("No devices found.");
            return;
        }

        var nameWidth = Math.Max("Name".Length, devices.Max(d => d.Name.Length));
        var directionWidth = Math.Max("Direction".Length, devices.Max(d => d.Direction.ToString().Length));
        var channelWidth = Math.Max("Channels".Length, devices.Max(d => d.ChannelCount.ToString(CultureInfo.InvariantCulture).Length));
        var sampleRateWidth = Math.Max("Sample Rate".Length, devices.Max(d => d.SampleRate.ToString(CultureInfo.InvariantCulture).Length));

        string FormatRow(string name, string direction, string channels, string sampleRate) =>
            $"{name.PadRight(nameWidth)}  {direction.PadRight(directionWidth)}  {channels.PadRight(channelWidth)}  {sampleRate.PadRight(sampleRateWidth)}";

        context.WriteLine(FormatRow("Name", "Direction", "Channels", "Sample Rate"));
        context.WriteLine(FormatRow(
            new string('-', nameWidth),
            new string('-', directionWidth),
            new string('-', channelWidth),
            new string('-', sampleRateWidth)));

        foreach (var device in devices)
        {
            context.WriteLine(FormatRow(
                device.Name,
                device.Direction.ToString(),
                device.ChannelCount.ToString(CultureInfo.InvariantCulture),
                device.SampleRate.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
