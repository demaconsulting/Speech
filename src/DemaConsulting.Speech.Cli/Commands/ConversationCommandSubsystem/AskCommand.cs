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
using DemaConsulting.Speech.Cli.Commands.RecognitionCommandSubsystem;
using DemaConsulting.Speech.Cli.Commands.SynthesisCommandSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Cli.Commands.ConversationCommandSubsystem;

/// <summary>
///     Implements the <c>ask</c> subcommand: speaks a prompt (supplied via <c>--text</c>,
///     <c>--file</c>, or piped stdin) through a known, installed synthesis model, then - once
///     playback finishes - immediately listens on a real microphone through a known, installed
///     recognition model for the reply, printing (and optionally writing to a text file) the
///     final recognized text.
/// </summary>
/// <remarks>
///     <para>
///     <b>Two-phase composition, not a new capability.</b> <c>ask</c> combines <c>speak</c>'s
///     synthesis-then-play flow (Phase 1) and <c>recognize --mic</c>'s mic-listen flow (Phase 2)
///     into one invocation, so an AI agent can hold a natural voice conversation with a person
///     using a single command instead of two separate CLI processes. No new library-level seam
///     member is introduced: Phase 1 reuses <see cref="ICliPlaybackDeviceSource"/> exactly as
///     <see cref="SpeakCommand"/> does, and Phase 2 reuses a directly-injected
///     <see cref="AudioDeviceFactory"/> exactly as <see cref="RecognizeCommand"/> does.
///     </para>
///     <para>
///     <b>Listen-termination rule.</b> Unlike <c>recognize --mic</c>, which listens indefinitely
///     until <c>--silence-timeout</c>/<c>Ctrl+C</c>, <c>ask</c>'s Phase 2 additionally stops as
///     soon as the <em>first final</em> recognition result arrives: this command models a single
///     bounded question/answer turn ("listen for the reply"), not open-ended transcription, so
///     one final result is always enough to end the turn. <c>--silence-timeout</c>/
///     <c>--start-timeout</c> retain their exact <c>recognize --mic</c> meaning as a safety net
///     for a reply that never finishes (or never starts).
///     </para>
///     <para>
///     <b>No <c>--output-audio</c>/<c>--input</c>/<c>--interim</c>/<c>--final-only</c>/<c>--no-tags</c>.</b>
///     <c>ask</c> always plays the prompt through a real device (there is no WAV-file destination
///     option) and always listens through a real microphone (there is no WAV-file source option);
///     it never prints interim results, so there is nothing for <c>--interim</c>/<c>--final-only</c>
///     to select between; and it does not strip Natural Language Audio Tags, since a short prompt
///     does not carry the same tag-stripping concern <c>speak</c>'s general-purpose text does.
///     </para>
///     <para>
///     <b>Cancellation and disposal.</b> A single <see cref="Console.CancelKeyPress"/> handler,
///     installed once for the whole call, cancels a shared <see cref="CancellationTokenSource"/>
///     (aborting an in-flight <c>SpeakAsync</c> during Phase 1) and, once the recognizer has been
///     constructed, also calls <see cref="ISpeechRecognizer.Stop"/> and signals the same
///     <see cref="ManualResetEventSlim"/> the mic-wait blocks on, so <c>Ctrl+C</c> cancels
///     cleanly whether it lands during playback or during listening. The synthesizer, playback
///     device, recognizer, silence-timeout session, and output writer are each disposed exactly
///     once via nested <c>finally</c> blocks, mirroring <see cref="SpeakCommand"/>'s and
///     <see cref="RecognizeCommand"/>'s own disposal ordering.
///     </para>
/// </remarks>
internal static class AskCommand
{
    /// <summary>
    ///     Runs the <c>ask</c> subcommand against a real, composed
    ///     <see cref="SpeechModelCatalogAdapter"/>, <see cref="AudioDeviceFactoryPlaybackDeviceSource"/>,
    ///     and <see cref="AudioDeviceFactory"/>, wiring <c>Ctrl+C</c> to cooperative cancellation
    ///     for the duration of the call.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static void Run(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var catalog = CliModelCatalogFactory.Create(context);
        var deviceSource = new AudioDeviceFactoryPlaybackDeviceSource(new AudioDeviceFactory());
        var captureFactory = new AudioDeviceFactory();
        Run(context, catalog, deviceSource, captureFactory);
    }

    /// <summary>
    ///     Runs the <c>ask</c> subcommand against an injected catalog seam, playback-device
    ///     source, and capture-device factory, for unit testing without a real model catalog,
    ///     network access, or audio hardware.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <param name="catalog">The catalog seam to resolve both models through. Must not be null.</param>
    /// <param name="deviceSource">The playback-device seam to resolve a real playback device through. Must not be null.</param>
    /// <param name="captureFactory">The audio device factory to resolve a real capture device through. Must not be null.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="context"/>, <paramref name="catalog"/>,
    ///     <paramref name="deviceSource"/>, or <paramref name="captureFactory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown for any usage error: missing/unknown/wrong-role/not-downloaded model, conflicting
    ///     or missing text source, malformed/invalid <c>--tts-param</c>/<c>--stt-param</c> value,
    ///     or unknown <c>--playback-device</c>/<c>--capture-device</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when no real playback device or no real capture device is available.
    /// </exception>
    internal static void Run(
        Context context,
        ICliModelCatalog catalog,
        ICliPlaybackDeviceSource deviceSource,
        AudioDeviceFactory captureFactory)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(deviceSource);
        ArgumentNullException.ThrowIfNull(captureFactory);

        using var cancellationSource = new CancellationTokenSource();
        using var stopSignal = new ManualResetEventSlim(initialState: false);

        // Tracks the live recognizer (once Phase 2 has constructed one) so a Ctrl+C landing
        // during listening can stop it; guarded because the recognizer is created on this same
        // thread but a Ctrl+C handler runs on a separate thread and could otherwise observe a
        // torn/stale reference.
        ISpeechRecognizer? recognizer = null;
        var recognizerGate = new object();

        // Ask an in-flight speak/listen session to cancel cooperatively rather than letting the
        // runtime kill the process outright, so partially-played audio, an in-flight capture, and
        // any open output file are still disposed cleanly. Whether Ctrl+C lands during Phase 1 or
        // Phase 2, both cancellation paths are armed for the whole call.
        ConsoleCancelEventHandler onCancelKeyPress = (_, e) =>
        {
            e.Cancel = true;
            cancellationSource.Cancel();

            lock (recognizerGate)
            {
                recognizer?.Stop();
            }

            stopSignal.Set();
        };

        Console.CancelKeyPress += onCancelKeyPress;
        try
        {
            RunAsync(
                context,
                catalog,
                deviceSource,
                captureFactory,
                stopSignal,
                r =>
                {
                    lock (recognizerGate)
                    {
                        recognizer = r;
                    }
                },
                cancellationSource.Token).GetAwaiter().GetResult();
        }
        finally
        {
            Console.CancelKeyPress -= onCancelKeyPress;
        }
    }

    /// <summary>
    ///     Runs the <c>ask</c> subcommand's full text-source resolution, model resolution,
    ///     Phase 1 (speak) synthesis/playback, and Phase 2 (listen) capture/recognition logic.
    /// </summary>
    private static async Task RunAsync(
        Context context,
        ICliModelCatalog catalog,
        ICliPlaybackDeviceSource deviceSource,
        AudioDeviceFactory captureFactory,
        ManualResetEventSlim stopSignal,
        Action<ISpeechRecognizer?> onRecognizerCreated,
        CancellationToken cancellationToken)
    {
        var options = ParseArguments(context.CommandArgs);

        // Text-source resolution runs before model resolution, mirroring SpeakCommand's own
        // verified ordering: a usage error like conflicting --text/--file is a cheap,
        // model-independent input mistake that should be reported immediately.
        var text = ResolveText(options);

        var ttsDescriptor = ResolveTtsModel(catalog, options.TtsModelId);
        var sttDescriptor = ResolveSttModel(catalog, options.SttModelId);
        var ttsParameterValues = ParameterBagParser.Resolve(options.RawTtsParameters, ttsDescriptor.Model.Parameters, "--tts-param");
        var sttParameterValues = ParameterBagParser.Resolve(options.RawSttParameters, sttDescriptor.Model.Parameters, "--stt-param");

        // Phase 1: speak the prompt through a real playback device and wait for it to finish.
        var wasCanceled = await SpeakPromptAsync(context, catalog, deviceSource, ttsDescriptor, ttsParameterValues, text, options, cancellationToken)
            .ConfigureAwait(false);

        if (wasCanceled)
        {
            return;
        }

        // Phase 2: listen for the reply through a real capture device.
        var recognizedText = Listen(
            catalog,
            captureFactory,
            sttDescriptor,
            sttParameterValues,
            options,
            stopSignal,
            onRecognizerCreated);

        if (options.OutputPath is not null)
        {
            await File.WriteAllTextAsync(options.OutputPath, recognizedText + Environment.NewLine, CancellationToken.None)
                .ConfigureAwait(false);
            context.WriteLine($"Recognized text written to '{options.OutputPath}'.");
        }
        else
        {
            context.WriteLine(recognizedText);
        }
    }

    /// <summary>
    ///     Runs Phase 1: synthesizes and plays <paramref name="text"/> through a real playback
    ///     device resolved from <paramref name="deviceSource"/> (honoring <c>--playback-device</c>),
    ///     waiting for playback to finish (or be canceled) before returning.
    /// </summary>
    /// <returns><see langword="true"/> when playback was canceled; otherwise <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no real playback device is available.</exception>
    private static async Task<bool> SpeakPromptAsync(
        Context context,
        ICliModelCatalog catalog,
        ICliPlaybackDeviceSource deviceSource,
        SpeechModelDescriptor ttsDescriptor,
        IReadOnlyDictionary<string, object>? parameterValues,
        string text,
        AskOptions options,
        CancellationToken cancellationToken)
    {
        var knownDevices = deviceSource.PlaybackProbe.Enumerate();
        var selection = DevicesTestCommand.ResolveDeviceSelectionOrThrow(knownDevices, options.PlaybackDeviceName, "playback");

        var playbackDevice = deviceSource.CreatePlaybackDevice(selection);
        if (!playbackDevice.IsAvailable)
        {
            throw new InvalidOperationException(
                "No audio playback device is available on this machine; cannot run 'ask'.");
        }

        try
        {
            var synthesizer = catalog.CreateSynthesizer(ttsDescriptor, playbackDevice, parameterValues);
            try
            {
                try
                {
                    await synthesizer.SpeakAsync(text, cancellationToken).ConfigureAwait(false);
                    return false;
                }
                catch (OperationCanceledException)
                {
                    context.WriteError("Speech was canceled.");
                    return true;
                }
            }
            finally
            {
                // Synthesizer disposal blocks until any in-flight playback has genuinely
                // finished quiescing, so it must be disposed before the playback device.
                synthesizer.Dispose();
            }
        }
        finally
        {
            (playbackDevice as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    ///     Runs Phase 2: constructs a real capture device from <paramref name="captureFactory"/>
    ///     (honoring <c>--capture-device</c>), constructs the recognizer, and listens until the
    ///     first final recognition result arrives, a silence/start timeout fires, or <c>Ctrl+C</c>
    ///     is pressed (signaled externally via <paramref name="stopSignal"/>).
    /// </summary>
    /// <returns>The final recognized text, or an empty string if the session ended with no final result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no real capture device is available.</exception>
    private static string Listen(
        ICliModelCatalog catalog,
        AudioDeviceFactory captureFactory,
        SpeechModelDescriptor sttDescriptor,
        IReadOnlyDictionary<string, object>? parameterValues,
        AskOptions options,
        ManualResetEventSlim stopSignal,
        Action<ISpeechRecognizer?> onRecognizerCreated)
    {
        var knownDevices = captureFactory.CaptureProbe.Enumerate();
        var selection = DevicesTestCommand.ResolveDeviceSelectionOrThrow(knownDevices, options.CaptureDeviceName, "capture");

        var captureDevice = captureFactory.CreateCaptureDevice(selection);
        if (!captureDevice.IsAvailable)
        {
            throw new InvalidOperationException(
                "No audio capture device is available on this machine; cannot run 'ask'.");
        }

        if (stopSignal.IsSet)
        {
            // Ctrl+C already landed during Phase 1: skip listening entirely rather than starting
            // a recognizer session that would be stopped again immediately.
            return string.Empty;
        }

        var recognizer = catalog.CreateRecognizer(sttDescriptor, captureDevice, parameterValues);
        onRecognizerCreated(recognizer);
        try
        {
            var recognizedText = string.Empty;
            EventHandler<SpeechRecognitionEvent> onResultReceived = (_, e) =>
            {
                if (!e.Result.IsFinal)
                {
                    return;
                }

                // "ask" models a single bounded question/answer turn: the first final result is
                // always enough to end the turn, unlike "recognize --mic"'s open-ended listening.
                recognizedText = e.Result.Text;
                recognizer.Stop();
                stopSignal.Set();
            };

            recognizer.ResultReceived += onResultReceived;
            try
            {
                SilenceTimeoutRecognizerSession? session = null;
                try
                {
                    if (options.SilenceTimeoutSeconds is { } seconds)
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

                    recognizer.Start();
                    stopSignal.Wait();
                }
                finally
                {
                    session?.Dispose();
                }
            }
            finally
            {
                recognizer.ResultReceived -= onResultReceived;
            }

            return recognizedText;
        }
        finally
        {
            onRecognizerCreated(null);
            recognizer.Dispose();
        }
    }

    /// <summary>
    ///     Resolves the requested <c>--tts-model</c> id against the catalog, validating that it
    ///     exists, has the synthesis role, and is downloaded.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     Thrown when the model id is unknown, its role is not synthesis, or it is not
    ///     downloaded yet.
    /// </exception>
    private static SpeechModelDescriptor ResolveTtsModel(ICliModelCatalog catalog, string modelId)
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
    ///     Resolves the requested <c>--stt-model</c> id against the catalog, validating that it
    ///     exists, has the recognition role, and is downloaded.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     Thrown when the model id is unknown, its role is not recognition, or it is not
    ///     downloaded yet.
    /// </exception>
    private static SpeechModelDescriptor ResolveSttModel(ICliModelCatalog catalog, string modelId)
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
    ///     Resolves the prompt text to speak from exactly one of <c>--text</c>, <c>--file</c>, or a
    ///     redirected stdin stream.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     Thrown when more than one text source was given, or when none was given and stdin is
    ///     not redirected.
    /// </exception>
    private static string ResolveText(AskOptions options)
    {
        var sourceCount = (options.Text is not null ? 1 : 0) +
            (options.FilePath is not null ? 1 : 0);

        if (sourceCount > 1)
        {
            throw new ArgumentException(
                "ask accepts only one of --text or --file, not both.",
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
                "ask requires text from --text, --file, or piped stdin.",
                nameof(options));
        }

        return Console.In.ReadToEnd();
    }

    /// <summary>
    ///     Parses <c>ask</c>'s own arguments: <c>--tts-model</c>, <c>--stt-model</c>, <c>--text</c>,
    ///     <c>--file</c>, <c>--playback-device</c>, <c>--capture-device</c>, repeatable
    ///     <c>--tts-param key=value</c>, repeatable <c>--stt-param key=value</c>,
    ///     <c>--silence-timeout</c>, <c>--start-timeout</c>, and <c>--output-text</c>.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     Thrown when <c>--tts-model</c>/<c>--stt-model</c> is missing, a flag's value is
    ///     missing or malformed, or an unsupported argument is given.
    /// </exception>
    internal static AskOptions ParseArguments(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? ttsModelId = null;
        string? sttModelId = null;
        string? text = null;
        string? filePath = null;
        string? playbackDeviceName = null;
        string? captureDeviceName = null;
        double? silenceTimeoutSeconds = null;
        double? startTimeoutSeconds = null;
        string? outputPath = null;
        var rawTtsParameters = new List<(string Key, string Value)>();
        var rawSttParameters = new List<(string Key, string Value)>();

        var index = 0;
        while (index < args.Count)
        {
            var arg = args[index++];
            switch (arg)
            {
                case "--tts-model":
                    ttsModelId = RequireValue(args, ref index, "--tts-model");
                    break;

                case "--stt-model":
                    sttModelId = RequireValue(args, ref index, "--stt-model");
                    break;

                case "--text":
                    text = RequireValue(args, ref index, "--text");
                    break;

                case "--file":
                    filePath = RequireValue(args, ref index, "--file");
                    break;

                case "--playback-device":
                    playbackDeviceName = RequireValue(args, ref index, "--playback-device");
                    break;

                case "--capture-device":
                    captureDeviceName = RequireValue(args, ref index, "--capture-device");
                    break;

                case "--tts-param":
                    var ttsToken = RequireValue(args, ref index, "--tts-param");
                    rawTtsParameters.Add(ParameterBagParser.ParseToken(ttsToken, "--tts-param"));
                    break;

                case "--stt-param":
                    var sttToken = RequireValue(args, ref index, "--stt-param");
                    rawSttParameters.Add(ParameterBagParser.ParseToken(sttToken, "--stt-param"));
                    break;

                case "--silence-timeout":
                    silenceTimeoutSeconds = RequireDoubleValue(args, ref index, "--silence-timeout");
                    break;

                case "--start-timeout":
                    startTimeoutSeconds = RequireDoubleValue(args, ref index, "--start-timeout");
                    break;

                case "--output-text":
                    outputPath = RequireValue(args, ref index, "--output-text");
                    break;

                default:
                    throw new ArgumentException($"Unsupported argument '{arg}' for 'ask'.", nameof(args));
            }
        }

        if (ttsModelId is null)
        {
            throw new ArgumentException("ask requires a --tts-model <id> argument.", nameof(args));
        }

        if (sttModelId is null)
        {
            throw new ArgumentException("ask requires a --stt-model <id> argument.", nameof(args));
        }

        return new AskOptions(
            ttsModelId,
            sttModelId,
            text,
            filePath,
            playbackDeviceName,
            captureDeviceName,
            rawTtsParameters,
            rawSttParameters,
            silenceTimeoutSeconds,
            startTimeoutSeconds,
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

    /// <summary>The parsed <c>ask</c> flags.</summary>
    /// <param name="TtsModelId">The requested synthesis model id (required).</param>
    /// <param name="SttModelId">The requested recognition model id (required).</param>
    /// <param name="Text">The prompt text supplied via <c>--text</c>, or <see langword="null"/>.</param>
    /// <param name="FilePath">The prompt file path supplied via <c>--file</c>, or <see langword="null"/>.</param>
    /// <param name="PlaybackDeviceName">The requested playback device name, or <see langword="null"/> for the system default.</param>
    /// <param name="CaptureDeviceName">The requested capture device name, or <see langword="null"/> for the system default.</param>
    /// <param name="RawTtsParameters">The raw, unresolved <c>--tts-param key=value</c> tokens, in the order given.</param>
    /// <param name="RawSttParameters">The raw, unresolved <c>--stt-param key=value</c> tokens, in the order given.</param>
    /// <param name="SilenceTimeoutSeconds">The idle timeout, in seconds, supplied via <c>--silence-timeout</c>, or <see langword="null"/> for none.</param>
    /// <param name="StartTimeoutSeconds">
    ///     The idle timeout, in seconds, used only before the first recognition result arrives,
    ///     supplied via <c>--start-timeout</c>, or <see langword="null"/> to default to
    ///     <paramref name="SilenceTimeoutSeconds"/>'s value. Only consulted when
    ///     <paramref name="SilenceTimeoutSeconds"/> is also given.
    /// </param>
    /// <param name="OutputPath">The text output path supplied via <c>--output-text</c>, or <see langword="null"/> to print to stdout.</param>
    internal sealed record AskOptions(
        string TtsModelId,
        string SttModelId,
        string? Text,
        string? FilePath,
        string? PlaybackDeviceName,
        string? CaptureDeviceName,
        IReadOnlyList<(string Key, string Value)> RawTtsParameters,
        IReadOnlyList<(string Key, string Value)> RawSttParameters,
        double? SilenceTimeoutSeconds,
        double? StartTimeoutSeconds,
        string? OutputPath);
}
