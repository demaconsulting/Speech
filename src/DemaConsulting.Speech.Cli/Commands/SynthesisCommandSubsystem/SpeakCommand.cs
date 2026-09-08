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
using DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Cli.Commands.SynthesisCommandSubsystem;

/// <summary>
///     Implements the <c>speak</c> subcommand: synthesizes text (supplied via <c>--text</c>,
///     <c>--file</c>, or piped stdin) through a known, installed synthesis model, either playing
///     it back on a real audio device or writing it to a WAV file via <c>--output</c>.
/// </summary>
/// <remarks>
///     <para>
///     <c>--device</c> is documented, and enforced by construction, to be ignored when
///     <c>--output</c> is given: an explicit file destination unambiguously wins over the
///     playback device selection, and no error is raised for supplying both.
///     </para>
///     <para>
///     <c>--no-tags</c> forces Natural Language Audio Tag stripping unconditionally, regardless
///     of the resolved model's declared <see cref="SpeechModelAudioTagSupport"/> - even a model
///     that natively supports tags has them stripped when this flag is given.
///     </para>
/// </remarks>
internal static class SpeakCommand
{
    /// <summary>
    ///     Runs the <c>speak</c> subcommand against a real, composed
    ///     <see cref="SpeechModelCatalogAdapter"/> and <see cref="AudioDeviceFactory"/>, wiring
    ///     <c>Ctrl+C</c> to cooperative cancellation for the duration of the call.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static void Run(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var catalog = CliModelCatalogFactory.Create(context);
        var factory = new AudioDeviceFactory();
        Run(context, catalog, factory);
    }

    /// <summary>
    ///     Runs the <c>speak</c> subcommand against an injected catalog seam and audio device
    ///     factory, for unit testing without a real model catalog, network access, or audio
    ///     hardware.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <param name="catalog">The catalog seam to resolve the model through. Must not be null.</param>
    /// <param name="factory">The audio device factory to resolve a real playback device through. Must not be null.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="context"/>, <paramref name="catalog"/>, or
    ///     <paramref name="factory"/> is <see langword="null"/>.
    /// </exception>
    internal static void Run(Context context, ICliModelCatalog catalog, AudioDeviceFactory factory)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(factory);

        using var cancellationSource = new CancellationTokenSource();

        // Ask an in-flight speak session to cancel cooperatively rather than letting the runtime
        // kill the process outright, so partially-played audio and any open output file are
        // still disposed cleanly.
        ConsoleCancelEventHandler onCancelKeyPress = (_, e) =>
        {
            e.Cancel = true;
            cancellationSource.Cancel();
        };

        Console.CancelKeyPress += onCancelKeyPress;
        try
        {
            RunAsync(context, catalog, factory, cancellationSource.Token).GetAwaiter().GetResult();
        }
        finally
        {
            Console.CancelKeyPress -= onCancelKeyPress;
        }
    }

    /// <summary>
    ///     Runs the <c>speak</c> subcommand's full text-source resolution, model resolution,
    ///     device/synthesizer composition, and playback/output logic.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <param name="catalog">The catalog seam to resolve the model through. Must not be null.</param>
    /// <param name="factory">The audio device factory to resolve a real playback device through. Must not be null.</param>
    /// <param name="cancellationToken">A token that, when canceled, aborts the in-progress speak session.</param>
    /// <returns>A task that completes once the speak session has finished, failed, or been canceled.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="context"/>, <paramref name="catalog"/>, or
    ///     <paramref name="factory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown for any usage error: missing/unknown/wrong-role/not-downloaded model, conflicting
    ///     or missing text source, malformed/invalid <c>--param</c> value, or unknown <c>--device</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when no real playback device is available and <c>--output</c> was not given.</exception>
    internal static async Task RunAsync(
        Context context,
        ICliModelCatalog catalog,
        AudioDeviceFactory factory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(factory);

        var options = ParseArguments(context.CommandArgs);

        // Text-source resolution runs before model resolution: a usage error like conflicting
        // --text/--file is a cheap, model-independent input mistake that should be reported
        // immediately, not masked by an unrelated "unknown model id" error.
        var text = ResolveText(options);
        if (options.NoTags)
        {
            text = StripAudioTags(text);
        }

        var descriptor = ResolveModel(catalog, options.ModelId);
        var parameterValues = ParameterBagParser.Resolve(options.RawParameters, descriptor.Model.Parameters);

        var playbackDevice = ResolvePlaybackDevice(catalog, factory, descriptor, options);
        try
        {
            var synthesizer = catalog.CreateSynthesizer(descriptor, playbackDevice, parameterValues);
            try
            {
                await synthesizer.SpeakAsync(text, cancellationToken).ConfigureAwait(false);
                context.WriteLine(options.OutputPath is null
                    ? "Speech playback finished."
                    : $"Speech written to '{options.OutputPath}'.");
            }
            catch (OperationCanceledException)
            {
                context.WriteError("Speech was canceled.");
            }
            finally
            {
                // Synthesizer disposal blocks until any in-flight playback has genuinely
                // finished quiescing, so it must be disposed before the playback device to
                // guarantee the device is never disposed out from under an in-flight write.
                synthesizer.Dispose();
            }
        }
        finally
        {
            // IAudioPlaybackDevice itself does not declare IDisposable (a real PortAudio-backed
            // device manages its own native stream lifecycle entirely through Start()/Stop()),
            // but WavFileAudioPlaybackDevice - used for --output - additionally implements
            // IDisposable to finalize its RIFF header; disposing it here (via the conditional
            // cast) is required, and is a safe no-op for any device that does not implement it.
            (playbackDevice as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    ///     Resolves the playback device for this session: a <see cref="WavFileAudioPlaybackDevice"/>
    ///     sized from the model's preferred audio format when <c>--output</c> was given, otherwise
    ///     a real device resolved from <paramref name="factory"/> (honoring <c>--device</c>).
    /// </summary>
    private static IAudioPlaybackDevice ResolvePlaybackDevice(
        ICliModelCatalog catalog,
        AudioDeviceFactory factory,
        SpeechModelDescriptor descriptor,
        SpeakOptions options)
    {
        if (options.OutputPath is not null)
        {
            var format = catalog.GetPreferredAudioFormat(descriptor);
            return new WavFileAudioPlaybackDevice(options.OutputPath, format.SampleRate, format.ChannelCount);
        }

        var knownDevices = factory.PlaybackProbe.Enumerate();
        var selection = DevicesTestCommand.ResolveDeviceSelectionOrThrow(knownDevices, options.DeviceName, "playback");

        var device = factory.CreatePlaybackDevice(selection);
        if (!device.IsAvailable)
        {
            throw new InvalidOperationException(
                "No audio playback device is available on this machine; cannot run 'speak'. " +
                "Use --output to write to a WAV file instead.");
        }

        return device;
    }

    /// <summary>
    ///     Resolves the requested model id against the catalog, validating that it exists, has
    ///     the synthesis role, and is downloaded.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     Thrown when the model id is unknown, its role is not synthesis, or it is not
    ///     downloaded yet.
    /// </exception>
    private static SpeechModelDescriptor ResolveModel(ICliModelCatalog catalog, string modelId)
    {
        var descriptor = catalog.Enumerate()
            .FirstOrDefault(candidate => string.Equals(candidate.Id, modelId, StringComparison.Ordinal));

        if (descriptor is null)
        {
            throw new ArgumentException(
                $"Unknown model id '{modelId}'. Use 'list-models' to see available models.",
                nameof(modelId));
        }

        if (descriptor.Role != SpeechModelRole.Synthesis)
        {
            throw new ArgumentException(
                $"Model '{modelId}' is a {descriptor.Role} model, not a synthesis model. " +
                "Use 'list-models --role tts' to see available synthesis models.",
                nameof(modelId));
        }

        if (descriptor.State != SpeechModelState.Downloaded)
        {
            throw new ArgumentException(
                $"Model '{modelId}' is not downloaded yet. Run 'download {modelId}' first.",
                nameof(modelId));
        }

        return descriptor;
    }

    /// <summary>
    ///     Resolves the text to speak from exactly one of <c>--text</c>, <c>--file</c>, or a
    ///     redirected stdin stream.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     Thrown when more than one text source was given, or when none was given and stdin is
    ///     not redirected.
    /// </exception>
    private static string ResolveText(SpeakOptions options)
    {
        var sourceCount = (options.Text is not null ? 1 : 0) +
            (options.FilePath is not null ? 1 : 0);

        if (sourceCount > 1)
        {
            throw new ArgumentException(
                "speak accepts only one of --text or --file, not both.",
                nameof(options));
        }

        if (options.Text is not null)
        {
            return options.Text;
        }

        if (options.FilePath is not null)
        {
            return File.ReadAllText(options.FilePath);
        }

        if (!Console.IsInputRedirected)
        {
            throw new ArgumentException(
                "speak requires text from --text, --file, or piped stdin.",
                nameof(options));
        }

        return Console.In.ReadToEnd();
    }

    /// <summary>
    ///     Strips every Natural Language Audio Tag from <paramref name="text"/>, keeping only the
    ///     literal narration text, per <c>--no-tags</c>'s forced-stripping contract.
    /// </summary>
    private static string StripAudioTags(string text) =>
        string.Concat(AudioTagParser.Parse(text)
            .Where(span => span.Kind == TaggedTextSpanKind.PlainText)
            .Select(span => span.Text));

    /// <summary>
    ///     Parses <c>speak</c>'s own arguments: <c>--model</c>, <c>--text</c>, <c>--file</c>,
    ///     <c>--output</c>, <c>--device</c>, <c>--no-tags</c>, and repeatable <c>--param key=value</c>.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     Thrown when <c>--model</c> is missing, a flag's value is missing, or an unsupported
    ///     argument is given.
    /// </exception>
    internal static SpeakOptions ParseArguments(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? modelId = null;
        string? text = null;
        string? filePath = null;
        string? outputPath = null;
        string? deviceName = null;
        var noTags = false;
        var rawParameters = new List<(string Key, string Value)>();

        var index = 0;
        while (index < args.Count)
        {
            var arg = args[index++];
            switch (arg)
            {
                case "--model":
                    modelId = RequireValue(args, ref index, "--model");
                    break;

                case "--text":
                    text = RequireValue(args, ref index, "--text");
                    break;

                case "--file":
                    filePath = RequireValue(args, ref index, "--file");
                    break;

                case "--output":
                    outputPath = RequireValue(args, ref index, "--output");
                    break;

                case "--device":
                    deviceName = RequireValue(args, ref index, "--device");
                    break;

                case "--no-tags":
                    noTags = true;
                    break;

                case "--param":
                    var token = RequireValue(args, ref index, "--param");
                    rawParameters.Add(ParameterBagParser.ParseToken(token));
                    break;

                default:
                    throw new ArgumentException($"Unsupported argument '{arg}' for 'speak'.", nameof(args));
            }
        }

        if (modelId is null)
        {
            throw new ArgumentException("speak requires a --model <id> argument.", nameof(args));
        }

        return new SpeakOptions(modelId, text, filePath, outputPath, deviceName, noTags, rawParameters);
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

    /// <summary>The parsed <c>speak</c> flags.</summary>
    /// <param name="ModelId">The requested model id (required).</param>
    /// <param name="Text">The text supplied via <c>--text</c>, or <see langword="null"/>.</param>
    /// <param name="FilePath">The file path supplied via <c>--file</c>, or <see langword="null"/>.</param>
    /// <param name="OutputPath">The WAV output path supplied via <c>--output</c>, or <see langword="null"/> for real playback.</param>
    /// <param name="DeviceName">The requested playback device name, or <see langword="null"/> for the system default. Ignored when <paramref name="OutputPath"/> is not <see langword="null"/>.</param>
    /// <param name="NoTags">Whether <c>--no-tags</c> was given.</param>
    /// <param name="RawParameters">The raw, unresolved <c>--param key=value</c> tokens, in the order given.</param>
    internal sealed record SpeakOptions(
        string ModelId,
        string? Text,
        string? FilePath,
        string? OutputPath,
        string? DeviceName,
        bool NoTags,
        IReadOnlyList<(string Key, string Value)> RawParameters);
}
