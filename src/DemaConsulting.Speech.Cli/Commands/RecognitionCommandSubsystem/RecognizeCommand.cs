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
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Cli.Commands.RecognitionCommandSubsystem;

/// <summary>
///     Implements the <c>recognize</c> subcommand: streams speech-to-text results from either a
///     pre-recorded mono WAV file (<c>--input</c>) or a real microphone (<c>--mic</c>) through a
///     known, installed recognition model, printing (and optionally writing to a text file) the
///     recognized text as it arrives.
/// </summary>
/// <remarks>
///     <para>
///     <b>File-input mode (<c>--input</c>) requires no explicit "wait until done" loop.</b>
///     <see cref="ISpeechRecognizer.Start"/> calls the supplied capture device's own
///     <c>Start()</c> synchronously (not on a background thread); a
///     <see cref="AudioSubsystem.WavFileAudioCaptureDevice"/>'s own <c>Start()</c> is itself fully
///     synchronous and blocking, delivering every frame before returning. This command subscribes
///     its own handler directly to the device instance's own <c>EndOfFileReached</c> event (a
///     member additional to <see cref="IAudioCaptureDevice"/>, not the recognizer's internal
///     subscription) and that handler calls <c>recognizer.Stop()</c> - which runs <b>reentrantly,
///     on the same thread, from inside <see cref="ISpeechRecognizer.Start"/>'s own call to the
///     device's <c>Start()</c></b>, immediately after the last frame is delivered and immediately
///     before the device's own <c>Start()</c> returns. <see cref="ISpeechRecognizer.Stop"/>'s
///     drain is a genuine, synchronous block (confirmed by reading
///     <c>SherpaOnnxSpeechRecognizer.StopCore</c>/<c>WaitForConsumer</c>, which calls
///     <c>consumerTask.GetAwaiter().GetResult()</c>), so by the time
///     <see cref="ISpeechRecognizer.Start"/> returns to this command, every result derived from
///     the whole file has already been raised and the recognizer has already fully stopped. No
///     settle-wait or fixed sleep is added anywhere in this design; none is needed.
///     </para>
///     <para>
///     <b>Mic-input mode (<c>--mic</c>)</b> blocks the calling thread on a
///     <see cref="ManualResetEventSlim"/> set either by a <c>Ctrl+C</c> handler or, when
///     <c>--silence-timeout</c> was given, by a <see cref="SilenceTimeoutRecognizerSession"/>'s
///     <c>TimedOut</c> event (the session itself already called <c>Stop()</c> before raising that
///     event). The session enforces two distinct idle windows: <c>--start-timeout</c> (defaulting
///     to <c>--silence-timeout</c>'s value when omitted) governs the grace period before any
///     result has arrived, and <c>--silence-timeout</c> governs every re-arm from the first
///     result onward.
///     </para>
///     <para>
///     <b>Disposal.</b> Neither <see cref="IAudioCaptureDevice"/> nor
///     <see cref="AudioSubsystem.WavFileAudioCaptureDevice"/> nor the real PortAudio-backed
///     capture device implement <see cref="IDisposable"/> (confirmed directly from all three
///     source files), so - unlike <c>speak</c>'s playback-device side - this command never needs
///     a conditional capture-device disposal cast. Only the recognizer and, in mic mode with a
///     silence timeout, the <see cref="SilenceTimeoutRecognizerSession"/> need disposal, both
///     handled in nested <c>finally</c> blocks so every exit path (EOF stop, silence-timeout stop,
///     <c>Ctrl+C</c>, or an error) disposes them exactly once.
///     </para>
///     <para>
///     <b><c>--interim</c>/<c>--final-only</c> console UX.</b> The default (neither flag) prints
///     both: an interim (<c>IsFinal == false</c>) result is written with a carriage-return
///     overwrite and no trailing newline, so a live console redraws the evolving hypothesis in
///     place, while a final result is written padded and newline-terminated, "settling" that line
///     before the next utterance's interim results begin overwriting again. This mirrors familiar
///     live-transcription UX without cluttering scripted/piped output with a per-line prefix; when
///     stdout is redirected the carriage-return behavior degrades gracefully to one line per
///     event, which is an accepted trade-off, not a defect. <c>--interim</c> prints only interim
///     results (still overwritten); <c>--final-only</c> prints only final results, one per line.
///     </para>
///     <para>
///     <b><c>--output &lt;text-path&gt;</c></b> writes only final results to the file, one line
///     each, flushed immediately - interim results are a live-console-only concept and are never
///     written to the file, regardless of <c>--interim</c>/<c>--final-only</c>'s effect on
///     console output. The file is opened once with overwrite (not append) semantics, consistent
///     with <c>speak --output</c>'s WAV semantics.
///     </para>
///     <para>
///     <c>--device</c> is not rejected when given alongside <c>--input</c>: it is simply inert in
///     that case (file mode never consults it), mirroring <c>speak</c>'s own documented precedent
///     that <c>--device</c> is silently ignored, not an error, when a file destination is also
///     given.
///     </para>
/// </remarks>
internal static class RecognizeCommand
{
    /// <summary>
    ///     Runs the <c>recognize</c> subcommand against a real, composed
    ///     <see cref="SpeechModelCatalogAdapter"/> and <see cref="AudioDeviceFactory"/>.
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
    ///     Runs the <c>recognize</c> subcommand against an injected catalog seam and audio device
    ///     factory, for unit testing without a real model catalog, network access, or audio
    ///     hardware.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <param name="catalog">The catalog seam to resolve the model through. Must not be null.</param>
    /// <param name="factory">The audio device factory to resolve a real capture device through. Must not be null.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="context"/>, <paramref name="catalog"/>, or
    ///     <paramref name="factory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown for any usage error: missing/unknown/wrong-role/not-downloaded model,
    ///     conflicting or missing input source, conflicting <c>--interim</c>/<c>--final-only</c>,
    ///     malformed/invalid <c>--param</c> value, or unknown <c>--device</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when no real capture device is available in mic mode.</exception>
    internal static void Run(Context context, ICliModelCatalog catalog, AudioDeviceFactory factory)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(factory);

        var options = ParseArguments(context.CommandArgs);

        // Flag mutual-exclusion checks run before model resolution: a malformed flag shape is a
        // cheaper, more fundamental usage mistake than an unknown model id, mirroring
        // SpeakCommand's own verified ordering (text-source mutual exclusion before model
        // resolution).
        ValidateInputSource(options);
        ValidateVerbosityFlags(options);

        var descriptor = ResolveModel(catalog, options.ModelId);
        var parameterValues = ParameterBagParser.Resolve(options.RawParameters, descriptor.Model.Parameters);

        using var stopSignal = new ManualResetEventSlim(initialState: false);
        var captureDevice = ResolveCaptureDevice(factory, options);

        StreamWriter? outputWriter = null;
        try
        {
            if (options.OutputPath is not null)
            {
                outputWriter = new StreamWriter(options.OutputPath, append: false) { AutoFlush = true };
            }

            var recognizer = catalog.CreateRecognizer(descriptor, captureDevice, parameterValues);
            var consoleLine = new ConsoleLineState();
            EventHandler<SpeechRecognitionEvent> onResultReceived =
                (_, e) => HandleResult(outputWriter, options, e, consoleLine);
            try
            {
                recognizer.ResultReceived += onResultReceived;
                SilenceTimeoutRecognizerSession? session = null;
                try
                {
                    if (options.Mic && options.SilenceTimeoutSeconds is { } seconds)
                    {
                        var startTimeout = options.StartTimeoutSeconds is { } startSeconds
                            ? TimeSpan.FromSeconds(startSeconds)
                            : (TimeSpan?)null;
                        session = new SilenceTimeoutRecognizerSession(
                            recognizer,
                            TimeSpan.FromSeconds(seconds),
                            startTimeout: startTimeout);
                        session.TimedOut += (_, _) => stopSignal.Set();
                    }

                    if (options.InputPath is not null && captureDevice is WavFileAudioCaptureDevice fileDevice)
                    {
                        fileDevice.EndOfFileReached += (_, _) => recognizer.Stop();
                    }

                    ConsoleCancelEventHandler onCancelKeyPress = (_, e) =>
                    {
                        e.Cancel = true;
                        recognizer.Stop();
                        stopSignal.Set();
                    };

                    Console.CancelKeyPress += onCancelKeyPress;
                    try
                    {
                        // File mode: Start() itself only returns once the whole file has been
                        // delivered and drained (see this type's remarks), so no wait is needed
                        // here. Mic mode: Start() returns quickly, so this thread blocks until
                        // Ctrl+C or a silence timeout signals stopSignal.
                        recognizer.Start();
                        if (options.Mic)
                        {
                            stopSignal.Wait();
                        }
                    }
                    finally
                    {
                        Console.CancelKeyPress -= onCancelKeyPress;
                    }
                }
                finally
                {
                    session?.Dispose();
                }

                // Settle any interim line still pending (e.g. a session that ends right after an
                // interim result with no final ever arriving) before printing the completion
                // message, so it never visually concatenates onto leftover interim text either.
                SettleConsoleLine(consoleLine);

                context.WriteLine(options.OutputPath is null
                    ? "Recognition finished."
                    : $"Recognition results written to '{options.OutputPath}'.");
            }
            finally
            {
                recognizer.ResultReceived -= onResultReceived;
                recognizer.Dispose();
            }
        }
        finally
        {
            outputWriter?.Dispose();
        }
    }

    /// <summary>
    ///     Prints (and, for a final result, optionally writes to the output file) one recognition
    ///     result, honoring <c>--interim</c>/<c>--final-only</c>. Any carriage-return-overwritten
    ///     interim line still pending (tracked via <paramref name="consoleLine"/>) is settled -
    ///     padded over and newline-terminated - before a final result is printed, so the final
    ///     result never visually concatenates onto leftover interim text. A shrinking interim
    ///     write (a hypothesis revised to something shorter than a previous one) is itself padded
    ///     over the longest interim text printed since the last settle, so stale trailing
    ///     characters from a longer prior write never remain visible.
    /// </summary>
    private static void HandleResult(
        StreamWriter? outputWriter,
        RecognizeOptions options,
        SpeechRecognitionEvent e,
        ConsoleLineState consoleLine)
    {
        var result = e.Result;

        if (result.IsFinal)
        {
            if (!options.InterimOnly)
            {
                // Settle any pending interim line before printing the final result, so it never
                // visually concatenates onto leftover interim text.
                SettleConsoleLine(consoleLine);
                Console.WriteLine(result.Text);
            }

            outputWriter?.WriteLine(result.Text);
        }
        else if (!options.FinalOnly)
        {
            var previousMaxLength = consoleLine.MaxPendingInterimLength;
            consoleLine.MaxPendingInterimLength = Math.Max(previousMaxLength, result.Text.Length);

            if (result.Text.Length < previousMaxLength)
            {
                // This hypothesis is shorter than the longest interim text printed since the
                // last settle: pad over the stale trailing characters, then return the cursor to
                // just past the new, shorter text so the visible line matches result.Text exactly.
                Console.Write($"\r{result.Text.PadRight(previousMaxLength)}\r{result.Text}");
            }
            else
            {
                Console.Write($"\r{result.Text}");
            }
        }
    }

    /// <summary>
    ///     Clears any carriage-return-overwritten interim text still pending on the current console
    ///     line (tracked via <paramref name="consoleLine"/>) by padding it with spaces and returning
    ///     the cursor to column 0, so that whatever is written next - a settled final result or a
    ///     completion message - starts on a clean line instead of visually concatenating onto the
    ///     interim text left behind. Pads over the longest interim text printed since the last
    ///     settle (not just the most recent write), so a shrinking-interim sequence never leaves
    ///     stale trailing characters visible. A no-op when no interim write is currently pending.
    /// </summary>
    /// <param name="consoleLine">The shared console-line tracking state for the current run.</param>
    private static void SettleConsoleLine(ConsoleLineState consoleLine)
    {
        if (consoleLine.MaxPendingInterimLength <= 0)
        {
            return;
        }

        Console.Write('\r' + new string(' ', consoleLine.MaxPendingInterimLength) + '\r');
        consoleLine.MaxPendingInterimLength = 0;
    }

    /// <summary>
    ///     Mutable state, shared across every <see cref="HandleResult"/> invocation for a single
    ///     <c>recognize</c> run and the completion-message write at the end of <c>Run</c>,
    ///     tracking the longest carriage-return-overwritten interim write left pending, un-settled,
    ///     on the current console line since the last settle.
    /// </summary>
    private sealed class ConsoleLineState
    {
        /// <summary>
        ///     Gets or sets the character length of the longest interim write that has not yet
        ///     been settled by a final result or completion message, or <c>0</c> when the console is
        ///     already on a clean line. Tracks the maximum length seen since the last settle (not
        ///     just the most recent write), so a shrinking interim sequence still pads over every
        ///     stale trailing character from a longer earlier write.
        /// </summary>
        public int MaxPendingInterimLength { get; set; }
    }

    /// <summary>
    ///     Validates that exactly one of <c>--input</c>/<c>--mic</c> was given.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when both or neither were given.</exception>
    private static void ValidateInputSource(RecognizeOptions options)
    {
        var sourceCount = (options.InputPath is not null ? 1 : 0) + (options.Mic ? 1 : 0);
        if (sourceCount != 1)
        {
            throw new ArgumentException(
                "recognize requires exactly one of --input <wav-path> or --mic.",
                nameof(options));
        }
    }

    /// <summary>
    ///     Validates that <c>--interim</c> and <c>--final-only</c> were not both given.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when both were given.</exception>
    private static void ValidateVerbosityFlags(RecognizeOptions options)
    {
        if (options.InterimOnly && options.FinalOnly)
        {
            throw new ArgumentException(
                "recognize accepts only one of --interim or --final-only, not both.",
                nameof(options));
        }
    }

    /// <summary>
    ///     Resolves the requested model id against the catalog, validating that it exists, has
    ///     the recognition role, and is downloaded.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     Thrown when the model id is unknown, its role is not recognition, or it is not
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

        if (descriptor.Role != SpeechModelRole.Recognition)
        {
            throw new ArgumentException(
                $"Model '{modelId}' is a {descriptor.Role} model, not a recognition model. " +
                "Use 'list-models --role stt' to see available recognition models.",
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
    ///     Resolves the capture device for this session: a <see cref="WavFileAudioCaptureDevice"/>
    ///     over the given file when <c>--input</c> was given, otherwise a real device resolved
    ///     from <paramref name="factory"/> (honoring <c>--device</c>).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <c>--device</c> names an unknown capture device.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no real capture device is available.</exception>
    private static IAudioCaptureDevice ResolveCaptureDevice(AudioDeviceFactory factory, RecognizeOptions options)
    {
        if (options.InputPath is not null)
        {
            return new WavFileAudioCaptureDevice(options.InputPath);
        }

        var knownDevices = factory.CaptureProbe.Enumerate();
        var selection = DevicesTestCommand.ResolveDeviceSelectionOrThrow(knownDevices, options.DeviceName, "capture");

        var device = factory.CreateCaptureDevice(selection);
        if (!device.IsAvailable)
        {
            throw new InvalidOperationException(
                "No audio capture device is available on this machine; cannot run 'recognize'. " +
                "Use --input to recognize from a WAV file instead.");
        }

        return device;
    }

    /// <summary>
    ///     Parses <c>recognize</c>'s own arguments: <c>--model</c>, <c>--input</c>, <c>--mic</c>,
    ///     <c>--device</c>, <c>--silence-timeout</c>, <c>--start-timeout</c>, repeatable
    ///     <c>--param key=value</c>, <c>--interim</c>, <c>--final-only</c>, and <c>--output</c>.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     Thrown when <c>--model</c> is missing, a flag's value is missing or malformed, or an
    ///     unsupported argument is given.
    /// </exception>
    internal static RecognizeOptions ParseArguments(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? modelId = null;
        string? inputPath = null;
        var mic = false;
        string? deviceName = null;
        double? silenceTimeoutSeconds = null;
        double? startTimeoutSeconds = null;
        var interim = false;
        var finalOnly = false;
        string? outputPath = null;
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

                case "--input":
                    inputPath = RequireValue(args, ref index, "--input");
                    break;

                case "--mic":
                    mic = true;
                    break;

                case "--device":
                    deviceName = RequireValue(args, ref index, "--device");
                    break;

                case "--silence-timeout":
                    silenceTimeoutSeconds = RequireDoubleValue(args, ref index, "--silence-timeout");
                    break;

                case "--start-timeout":
                    startTimeoutSeconds = RequireDoubleValue(args, ref index, "--start-timeout");
                    break;

                case "--param":
                    var token = RequireValue(args, ref index, "--param");
                    rawParameters.Add(ParameterBagParser.ParseToken(token));
                    break;

                case "--interim":
                    interim = true;
                    break;

                case "--final-only":
                    finalOnly = true;
                    break;

                case "--output":
                    outputPath = RequireValue(args, ref index, "--output");
                    break;

                default:
                    throw new ArgumentException($"Unsupported argument '{arg}' for 'recognize'.", nameof(args));
            }
        }

        if (modelId is null)
        {
            throw new ArgumentException("recognize requires a --model <id> argument.", nameof(args));
        }

        return new RecognizeOptions(
            modelId,
            inputPath,
            mic,
            deviceName,
            silenceTimeoutSeconds,
            startTimeoutSeconds,
            rawParameters,
            interim,
            finalOnly,
            outputPath);
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
    ///     Gets the value following a flag, parsed as a positive <see cref="double"/>, throwing a
    ///     clean <see cref="ArgumentException"/> when none was supplied or it is not a positive number.
    /// </summary>
    private static double RequireDoubleValue(IReadOnlyList<string> args, ref int index, string flag)
    {
        var value = RequireValue(args, ref index, flag);
        if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ||
            parsed <= 0.0)
        {
            throw new ArgumentException($"{flag} requires a positive number of seconds, not '{value}'.", nameof(args));
        }

        return parsed;
    }

    /// <summary>The parsed <c>recognize</c> flags.</summary>
    /// <param name="ModelId">The requested model id (required).</param>
    /// <param name="InputPath">The WAV file path supplied via <c>--input</c>, or <see langword="null"/> for mic mode.</param>
    /// <param name="Mic">Whether <c>--mic</c> was given.</param>
    /// <param name="DeviceName">The requested capture device name, or <see langword="null"/> for the system default. Only consulted in mic mode.</param>
    /// <param name="SilenceTimeoutSeconds">The idle timeout, in seconds, supplied via <c>--silence-timeout</c>, or <see langword="null"/> for none.</param>
    /// <param name="StartTimeoutSeconds">
    ///     The idle timeout, in seconds, used only before the first recognition result arrives,
    ///     supplied via <c>--start-timeout</c>, or <see langword="null"/> to default to
    ///     <paramref name="SilenceTimeoutSeconds"/>'s value. Only consulted when
    ///     <paramref name="SilenceTimeoutSeconds"/> is also given, in mic mode.
    /// </param>
    /// <param name="RawParameters">The raw, unresolved <c>--param key=value</c> tokens, in the order given.</param>
    /// <param name="InterimOnly">Whether <c>--interim</c> was given.</param>
    /// <param name="FinalOnly">Whether <c>--final-only</c> was given.</param>
    /// <param name="OutputPath">The text output path supplied via <c>--output</c>, or <see langword="null"/> for console-only output.</param>
    internal sealed record RecognizeOptions(
        string ModelId,
        string? InputPath,
        bool Mic,
        string? DeviceName,
        double? SilenceTimeoutSeconds,
        double? StartTimeoutSeconds,
        IReadOnlyList<(string Key, string Value)> RawParameters,
        bool InterimOnly,
        bool FinalOnly,
        string? OutputPath);
}
