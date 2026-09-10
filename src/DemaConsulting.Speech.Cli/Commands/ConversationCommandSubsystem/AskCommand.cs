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
///     <see cref="SpeakCommand"/> does, and Phase 2 resolves its capture device through
///     <see cref="ICliCaptureDeviceSource"/>, a CLI-owned seam mirroring
///     <see cref="ICliPlaybackDeviceSource"/> that lets unit tests substitute an in-memory fake
///     capture device instead of depending on a real, sealed <see cref="AudioDeviceFactory"/> and
///     real PortAudio hardware (unlike <see cref="RecognizeCommand"/>, which still resolves its
///     capture device directly from an injected <see cref="AudioDeviceFactory"/>).
///     </para>
///     <para>
///     <b>Listen-termination rule.</b> Unlike <c>recognize --mic</c>, which keeps listening
///     across multiple recognition results until <c>--silence-timeout</c> elapses or
///     <c>Ctrl+C</c> is pressed, <c>ask</c>'s Phase 2 additionally stops as
///     soon as the <em>first final</em> recognition result arrives: this command models a single
///     bounded question/answer turn ("listen for the reply"), not open-ended transcription, so
///     one final result is always enough to end the turn. <c>--silence-timeout</c>/
///     <c>--start-timeout</c> retain their exact <c>recognize --mic</c> meaning (including
///     their 5-second/8-second defaults) as a safety net for a reply that never finishes (or
///     never starts).
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
///     <b>Recognizer pre-warming.</b> Phase 2's recognizer - including resolving its capture
///     device - is constructed concurrently with Phase 1's speak/playback wait, via a background
///     <see cref="Task"/> kicked off immediately after both phases' parameters are resolved,
///     rather than only once playback finishes: loading a recognition model into native memory
///     is the expensive step (see <see cref="SpeechRecognizerFactory"/>'s own remarks), so
///     overlapping that load with the time a person spends listening to the
///     prompt removes an otherwise-unavoidable turnaround gap between finishing speaking and
///     starting to listen. Only the model *load* is pre-warmed: <see cref="ISpeechRecognizer.Start"/>
///     (which begins real microphone capture) is still called only once Phase 2 genuinely
///     begins, so pre-warming never captures audio while the prompt is still being spoken. A
///     pre-warm failure (an unknown/unavailable capture device, an invalid <c>--stt-param</c>
///     value, etc.) is not thrown from the background task itself; it surfaces when <c>RunAsync</c>
///     awaits the pre-warm task at the start of Phase 2, with the same exception type and message
///     a synchronous failure would have produced.
///     </para>
///     <para>
///     <b>Cancellation and disposal.</b> A single <see cref="Console.CancelKeyPress"/> handler,
///     installed once for the whole call, cancels a shared <see cref="CancellationTokenSource"/>
///     (aborting an in-flight <c>SpeakAsync</c> during Phase 1) and, once the recognizer has been
///     constructed, also calls <see cref="ISpeechRecognizer.Stop"/> and signals the same
///     <see cref="ManualResetEventSlim"/> the mic-wait blocks on, so <c>Ctrl+C</c> cancels
///     cleanly whether it lands during playback or during listening. When Phase 1 is canceled or
///     throws, the pre-warmed recognizer - which may already have been constructed by the
///     concurrent background task, or may still be in flight - is never awaited synchronously by
///     <c>RunAsync</c>: doing so would block <c>Ctrl+C</c>/fast-failure responsiveness on the
///     expensive model-load step finishing. Instead a background continuation observes the
///     pre-warm task's eventual result (or exception) and disposes any recognizer it produces,
///     so <c>RunAsync</c> returns promptly while the recognizer is still guaranteed to be disposed
///     - just asynchronously - rather than leaked or left as an unobserved faulted
///     <see cref="Task"/>. Because a final recognition result, a silence/start timeout, and
///     <c>Ctrl+C</c> all unblock the same <see cref="ManualResetEventSlim"/> identically,
///     <c>Listen</c> re-checks the shared <see cref="CancellationToken"/> (only ever canceled by
///     the <c>Ctrl+C</c> handler, never by a timeout or a normal final result) once it unblocks,
///     so a genuine <c>Ctrl+C</c> during Phase 2 is reported the same way a Phase 1 cancellation
///     is - via <see cref="Cli.Context.WriteError"/> - rather than silently written out as an
///     empty, successful result. <c>RunAsync</c> re-checks that same shared
///     <see cref="CancellationToken"/> once more immediately after <c>Listen</c> returns, before
///     writing/printing the recognized text, to close a narrow race where <c>Ctrl+C</c> lands
///     after <c>Listen</c> has already unblocked with <c>listenWasCanceled == false</c> but before
///     the result is written out; this final check is reported and handled identically to the
///     Phase 1/Phase 2 cancellation cases. The synthesizer, playback device, recognizer,
///     silence-timeout session, and output writer are each disposed exactly once via nested
///     <c>finally</c> blocks, mirroring <see cref="SpeakCommand"/>'s and
///     <see cref="RecognizeCommand"/>'s own disposal ordering.
///     </para>
/// </remarks>
internal static class AskCommand
{
    /// <summary>
    ///     The shared cancellation message reported when an <c>ask</c> session is stopped
    ///     cooperatively via <c>Ctrl+C</c> or a cancellation token.
    /// </summary>
    private const string SpeechCanceledMessage = "Speech was canceled.";

    /// <summary>
    ///     The repeatable flag used to set a <c>speak</c>-phase (TTS) model parameter.
    /// </summary>
    private const string TtsParamFlag = "--tts-param";

    /// <summary>
    ///     The repeatable flag used to set a <c>listen</c>-phase (STT) model parameter.
    /// </summary>
    private const string SttParamFlag = "--stt-param";

    /// <summary>
    ///     The default <c>--start-timeout</c> value, in seconds, used when the flag is omitted.
    ///     Without a bounded default, an <c>ask</c> reply that never begins would block forever
    ///     with only <c>Ctrl+C</c> as an escape hatch.
    /// </summary>
    private const double DefaultStartTimeoutSeconds = 8.0;

    /// <summary>
    ///     The default <c>--silence-timeout</c> value, in seconds, used when the flag is omitted.
    ///     Without a bounded default, an <c>ask</c> reply that never finishes would block forever
    ///     with only <c>Ctrl+C</c> as an escape hatch.
    /// </summary>
    private const double DefaultSilenceTimeoutSeconds = 5.0;

    /// <summary>
    ///     Runs the <c>ask</c> subcommand against a real, composed
    ///     <see cref="SpeechModelCatalogAdapter"/>, <see cref="AudioDeviceFactoryPlaybackDeviceSource"/>,
    ///     and <see cref="AudioDeviceFactoryCaptureDeviceSource"/>, wiring <c>Ctrl+C</c> to
    ///     cooperative cancellation for the duration of the call.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static void Run(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var catalog = CliModelCatalogFactory.Create(context);
        var factory = new AudioDeviceFactory();
        var deviceSource = new AudioDeviceFactoryPlaybackDeviceSource(factory);
        var captureSource = new AudioDeviceFactoryCaptureDeviceSource(factory);
        Run(context, catalog, deviceSource, captureSource);
    }

    /// <summary>
    ///     Runs the <c>ask</c> subcommand against an injected catalog seam, playback-device
    ///     source, and capture-device source, for unit testing without a real model catalog,
    ///     network access, or audio hardware.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <param name="catalog">The catalog seam to resolve both models through. Must not be null.</param>
    /// <param name="deviceSource">The playback-device seam to resolve a real playback device through. Must not be null.</param>
    /// <param name="captureSource">The capture-device seam to resolve a real capture device through. Must not be null.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="context"/>, <paramref name="catalog"/>,
    ///     <paramref name="deviceSource"/>, or <paramref name="captureSource"/> is <see langword="null"/>.
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
        ICliCaptureDeviceSource captureSource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(deviceSource);
        ArgumentNullException.ThrowIfNull(captureSource);

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
                captureSource,
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
    ///     Bundles the invocation-scoped dependencies shared by both Phase 1 (speak) and Phase 2
    ///     (listen), so <see cref="SpeakPromptAsync"/> and <see cref="Listen"/> stay under this
    ///     repository's parameter-count guideline without changing any public API surface.
    /// </summary>
    /// <param name="Context">The invocation context.</param>
    /// <param name="Catalog">The catalog seam to resolve models through.</param>
    /// <param name="Options">The parsed <c>ask</c> command-line options.</param>
    /// <param name="CancellationToken">The cooperative-cancellation token spanning both phases.</param>
    private readonly record struct AskInvocation(
        Context Context,
        ICliModelCatalog Catalog,
        AskOptions Options,
        CancellationToken CancellationToken);

    /// <summary>
    ///     Runs the <c>ask</c> subcommand's full text-source resolution, model resolution,
    ///     Phase 1 (speak) synthesis/playback, and Phase 2 (listen) capture/recognition logic.
    /// </summary>
    /// <remarks>
    ///     Marked <see langword="internal"/> (rather than <see langword="private"/>) so unit tests
    ///     can drive Phase 2's <c>Ctrl+C</c>-during-listen cancellation race directly: a test
    ///     supplies its own <paramref name="stopSignal"/> and <paramref name="cancellationToken"/>
    ///     (from a <see cref="CancellationTokenSource"/> the test owns), and can cancel that
    ///     source and set <paramref name="stopSignal"/> from within a fake recognizer's
    ///     <c>Start()</c> callback to simulate the exact interleaving the real
    ///     <see cref="Console.CancelKeyPress"/> handler produces, without depending on a real
    ///     console signal.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="context"/>, <paramref name="catalog"/>,
    ///     <paramref name="deviceSource"/>, <paramref name="captureSource"/>,
    ///     <paramref name="stopSignal"/>, or <paramref name="onRecognizerCreated"/> is
    ///     <see langword="null"/>.
    /// </exception>
    internal static async Task RunAsync(
        Context context,
        ICliModelCatalog catalog,
        ICliPlaybackDeviceSource deviceSource,
        ICliCaptureDeviceSource captureSource,
        ManualResetEventSlim stopSignal,
        Action<ISpeechRecognizer?> onRecognizerCreated,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(deviceSource);
        ArgumentNullException.ThrowIfNull(captureSource);
        ArgumentNullException.ThrowIfNull(stopSignal);
        ArgumentNullException.ThrowIfNull(onRecognizerCreated);

        var options = ParseArguments(context.CommandArgs);

        // Text-source resolution runs before model resolution, mirroring SpeakCommand's own
        // verified ordering: a usage error like conflicting --text/--file is a cheap,
        // model-independent input mistake that should be reported immediately.
        var text = ResolveText(options);

        var ttsDescriptor = ResolveTtsModel(catalog, options.TtsModelId);
        var sttDescriptor = ResolveSttModel(catalog, options.SttModelId);
        var ttsParameterValues = ParameterBagParser.Resolve(options.RawTtsParameters, ttsDescriptor.Model.Parameters, TtsParamFlag);
        var sttParameterValues = ParameterBagParser.Resolve(options.RawSttParameters, sttDescriptor.Model.Parameters, SttParamFlag);

        var invocation = new AskInvocation(context, catalog, options, cancellationToken);

        // Kick off Phase 2's recognizer construction (including its capture-device resolution)
        // on a background task now, so the expensive model-load step overlaps with Phase 1's
        // speak/playback wait below instead of only starting once playback finishes - closing the
        // turnaround-gap latency between finishing speaking and starting to listen. Real
        // microphone capture (Start()) still only begins once Phase 2 genuinely starts, inside
        // Listen. Only the "adopt the pre-warm result" exit path below (Phase 2 genuinely
        // starting) awaits this task directly. The "Phase 1 canceled" and "Phase 1 (or anything
        // between here and adopting the pre-warm result) threw" exit paths deliberately do NOT
        // await this task - blocking RunAsync's return on the expensive model-load finishing
        // would delay Ctrl+C/fast-failure responsiveness for no benefit - and instead hand it to
        // DisposePrewarmedRecognizerAsync fire-and-forget: that method's own continuation still
        // always observes this task's result/exception and disposes any recognizer it produces,
        // just asynchronously in the background rather than before RunAsync returns.
        var prewarmTask = Task.Run(
            () => PrewarmRecognizer(invocation, captureSource, sttDescriptor, sttParameterValues),
            CancellationToken.None);

        ISpeechRecognizer recognizer;
        try
        {
            // Phase 1: speak the prompt through a real playback device and wait for it to finish.
            var wasCanceled = await SpeakPromptAsync(invocation, deviceSource, ttsDescriptor, ttsParameterValues, text)
                .ConfigureAwait(false);

            if (wasCanceled)
            {
                // Phase 1 was canceled, so Phase 2 never runs: the concurrently pre-warmed
                // recognizer (whether already constructed, still in flight, or never going to
                // succeed) must still eventually be disposed, but RunAsync must not block its own
                // return on the (possibly still-loading) model finishing - that would delay
                // Ctrl+C responsiveness for exactly the scenario where instant feedback matters
                // most. Hand it off fire-and-forget: DisposePrewarmedRecognizerAsync's own
                // continuation disposes it once construction eventually completes.
                _ = DisposePrewarmedRecognizerAsync(prewarmTask);
                return;
            }

            // Adopt the pre-warm task's result now that Phase 2 is genuinely starting: this
            // re-throws (with its original type and message) any failure PrewarmRecognizer
            // raised - an unknown/unavailable capture device, an invalid --stt-param value,
            // etc. - at Phase 2's start, exactly as a synchronous call to the same logic would
            // have. Once assigned here, Listen's own finally block takes ownership of disposing
            // this recognizer exactly once.
            recognizer = await prewarmTask.ConfigureAwait(false);
        }
        // Intentionally broad: this is the command-level fail-fast boundary around Phase 1 and
        // recognizer pre-warm adoption, so any exception must preserve its original type/message
        // while still handing off the concurrently created recognizer for asynchronous cleanup.
        catch (Exception)
        {
            // SpeakPromptAsync (or any resolution logic reachable above, e.g. an unknown
            // --playback-device, or PrewarmRecognizer's own failure surfacing when its result is
            // adopted) threw instead of returning a normal wasCanceled result: the pre-warmed
            // recognizer - whether already constructed, still in flight, or never going to
            // succeed - must still eventually be disposed and prewarmTask's result/exception must
            // still eventually be observed, but RunAsync must not block this fast-failure exit on
            // the (possibly still-loading) model finishing. Hand it off fire-and-forget, same as
            // the Phase 1 cancellation path above; this is a no-op once it runs if prewarmTask
            // never produces a recognizer. Rethrow immediately to preserve the original
            // exception's type, message, and stack trace unchanged.
            _ = DisposePrewarmedRecognizerAsync(prewarmTask);
            throw;
        }

        // Phase 2: listen for the reply through the already-constructed recognizer.
        var (recognizedText, listenWasCanceled) = Listen(
            invocation,
            recognizer,
            stopSignal,
            onRecognizerCreated);

        if (listenWasCanceled)
        {
            return;
        }

        // Listen() itself completed without observing cancellation, but Ctrl+C can still land in
        // the narrow window between here and either output path below: each path re-checks the
        // shared token immediately before it actually writes/emits the reply, so that race is
        // reported identically to a cancellation observed inside Listen(), rather than silently
        // succeeding.
        if (options.OutputPath is not null)
        {
            var fileContents = recognizedText.Length == 0
                ? string.Empty
                : recognizedText + Environment.NewLine;

            // Re-checked immediately before the write itself (not just once, earlier in this
            // method) so a Ctrl+C landing right up to this point is still observed instead of
            // silently writing the file and reporting success.
            if (cancellationToken.IsCancellationRequested)
            {
                context.WriteError(SpeechCanceledMessage);
                return;
            }

            try
            {
                // Pass the real token through so a Ctrl+C landing during the write itself is
                // observed instead of being silently ignored: every other cancellation exit point
                // in this method is cooperative (check IsCancellationRequested, report, and
                // return), so mirror that exact behavior here rather than letting the write
                // complete and falsely report success.
                await File.WriteAllTextAsync(options.OutputPath, fileContents, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                context.WriteError(SpeechCanceledMessage);
                return;
            }

            context.WriteLine($"Recognized text written to '{options.OutputPath}'.");
        }
        else
        {
            // Re-checked immediately before emitting to stdout (not just once, earlier in this
            // method) so a Ctrl+C landing right up to this point is still observed instead of
            // silently emitting the reply and reporting success.
            if (cancellationToken.IsCancellationRequested)
            {
                context.WriteError(SpeechCanceledMessage);
                return;
            }

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
        AskInvocation invocation,
        ICliPlaybackDeviceSource deviceSource,
        SpeechModelDescriptor ttsDescriptor,
        IReadOnlyDictionary<string, object>? parameterValues,
        string text)
    {
        var context = invocation.Context;
        var catalog = invocation.Catalog;
        var options = invocation.Options;
        var cancellationToken = invocation.CancellationToken;

        var knownDevices = deviceSource.PlaybackProbe.Enumerate();
        var selection = DevicesTestCommand.ResolveDeviceSelectionOrThrow(knownDevices, options.PlaybackDeviceName, "playback");

        var playbackDevice = deviceSource.CreatePlaybackDevice(selection);
        if (!playbackDevice.IsAvailable)
        {
            throw new InvalidOperationException(
                "No audio playback device is available on this machine; cannot run 'ask'.");
        }

        using var playbackDeviceLease = playbackDevice as IDisposable;
        using var synthesizer = catalog.CreateSynthesizer(ttsDescriptor, playbackDevice, parameterValues);

        try
        {
            await synthesizer.SpeakAsync(text, cancellationToken).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException)
        {
            context.WriteError(SpeechCanceledMessage);
            return true;
        }
    }

    /// <summary>
    ///     Constructs Phase 2's recognizer - resolving a real capture device from
    ///     <paramref name="captureSource"/> (honoring <c>--capture-device</c>) first, since
    ///     <see cref="ICliModelCatalog.CreateRecognizer"/> requires an already-constructed
    ///     <see cref="AudioSubsystem.IAudioCaptureDevice"/> - without starting real microphone
    ///     capture. Run on a background <see cref="Task"/> concurrently with Phase 1's
    ///     speak/playback wait (see the class remarks' "Recognizer pre-warming" paragraph) so the
    ///     expensive model-load step overlaps with the time a person spends listening to the
    ///     prompt, rather than only starting once playback finishes.
    /// </summary>
    /// <returns>The constructed recognizer, not yet started.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no real capture device is available.</exception>
    private static ISpeechRecognizer PrewarmRecognizer(
        AskInvocation invocation,
        ICliCaptureDeviceSource captureSource,
        SpeechModelDescriptor sttDescriptor,
        IReadOnlyDictionary<string, object>? parameterValues)
    {
        var catalog = invocation.Catalog;
        var options = invocation.Options;

        var knownDevices = captureSource.CaptureProbe.Enumerate();
        var selection = DevicesTestCommand.ResolveDeviceSelectionOrThrow(knownDevices, options.CaptureDeviceName, "capture");

        var captureDevice = captureSource.CreateCaptureDevice(selection);
        if (!captureDevice.IsAvailable)
        {
            throw new InvalidOperationException(
                "No audio capture device is available on this machine; cannot run 'ask'.");
        }

        return catalog.CreateRecognizer(sttDescriptor, captureDevice, parameterValues);
    }

    /// <summary>
    ///     Fire-and-forget cleanup for a concurrently pre-warmed recognizer, for use when Phase 1
    ///     is canceled or throws and Phase 2 will never run: the recognizer
    ///     <paramref name="prewarmTask"/> produces (if construction succeeds at all) would
    ///     otherwise be leaked, and the task itself would otherwise go unobserved.
    /// </summary>
    /// <remarks>
    ///     Callers deliberately do not <see langword="await"/> the <see cref="Task"/> this method
    ///     returns: <paramref name="prewarmTask"/> represents the expensive recognizer model-load
    ///     step, so blocking on it here would delay <c>Ctrl+C</c>/fast-failure responsiveness
    ///     until that load finishes, even though the caller has already decided to exit. Instead,
    ///     this method's own <see langword="await"/> below attaches a continuation that runs
    ///     whenever <paramref name="prewarmTask"/> eventually completes - immediately, if it has
    ///     already finished by the time this method is called - disposing any recognizer it
    ///     produced and observing (via the <see langword="catch"/> below) any exception it
    ///     raised, so <paramref name="prewarmTask"/> is never left as an unobserved faulted
    ///     <see cref="Task"/> and a successfully constructed recognizer is never leaked - just
    ///     disposed asynchronously in the background instead of before the caller returns.
    /// </remarks>
    private static async Task DisposePrewarmedRecognizerAsync(Task<ISpeechRecognizer> prewarmTask)
    {
        try
        {
            using var recognizer = await prewarmTask.ConfigureAwait(false);
        }
        // Intentionally broad: this asynchronous cleanup boundary exists only to observe the
        // pre-warm task and dispose any successful recognizer result after the command has
        // already decided to exit, so a failed pre-warm is non-actionable noise here.
        catch (Exception)
        {
            // No recognizer was produced, so there is nothing to dispose.
        }
    }

    /// <summary>
    ///     Runs Phase 2: listens through the already-constructed <paramref name="recognizer"/>
    ///     until the first final recognition result arrives, a silence/start timeout fires, or
    ///     <c>Ctrl+C</c> is pressed (signaled externally via <paramref name="stopSignal"/>).
    /// </summary>
    /// <returns>
    ///     The final recognized text (an empty string if the session ended with no final result),
    ///     and whether the session ended because of a genuine <c>Ctrl+C</c> cancellation rather
    ///     than a legitimate empty result (silence/start timeout). When canceled, an error has
    ///     already been reported via <see cref="Cli.Context.WriteError"/> and the returned text
    ///     must not be written to <c>--output-text</c>/stdout.
    /// </returns>
    private static (string Text, bool WasCanceled) Listen(
        AskInvocation invocation,
        ISpeechRecognizer recognizer,
        ManualResetEventSlim stopSignal,
        Action<ISpeechRecognizer?> onRecognizerCreated)
    {
        var context = invocation.Context;
        var options = invocation.Options;
        var cancellationToken = invocation.CancellationToken;

        onRecognizerCreated(recognizer);
        using var recognizerLease = recognizer;
        try
        {
            if (stopSignal.IsSet)
            {
                // Ctrl+C already landed during Phase 1, during the concurrent recognizer
                // pre-warm, or in the narrow window between pre-warm finishing and Phase 2
                // starting: skip listening entirely rather than starting a recognizer session
                // that would be stopped again immediately. Only report this as a cancellation
                // when the shared token confirms Ctrl+C is the reason - the flag is otherwise
                // never set before this point.
                if (cancellationToken.IsCancellationRequested)
                {
                    context.WriteError(SpeechCanceledMessage);
                    return (string.Empty, true);
                }

                return (string.Empty, false);
            }

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
                // A silence-timeout session is always constructed - even when both flags are
                // omitted - so a reply that never starts or never finishes cannot block "ask"
                // forever with only Ctrl+C as an escape hatch.
                var sessionStartTimeout = TimeSpan.FromSeconds(options.StartTimeoutSeconds ?? DefaultStartTimeoutSeconds);
                using var session = new SilenceTimeoutRecognizerSession(
                    recognizer,
                    TimeSpan.FromSeconds(options.SilenceTimeoutSeconds ?? DefaultSilenceTimeoutSeconds),
                    startTimeout: sessionStartTimeout);

                session.TimedOut += (_, _) => stopSignal.Set();

                recognizer.Start();
                try
                {
                    stopSignal.Wait(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Ctrl+C canceled the shared token while waiting; whether or not
                    // stopSignal itself has already been set by the same handler is
                    // irrelevant here - cancellationToken.IsCancellationRequested below is
                    // what distinguishes this from a legitimate empty result. Stop the
                    // recognizer explicitly here (mirroring onResultReceived above) so the
                    // intent is obvious without requiring a reader to trace through
                    // ISpeechRecognizer's disposal-implies-stop contract; the recognizer is
                    // disposed unconditionally below regardless, so this call is redundant
                    // but harmless given Stop() is documented as idempotent.
                    recognizer.Stop();
                }
            }
            finally
            {
                recognizer.ResultReceived -= onResultReceived;
            }

            // stopSignal.Wait() above unblocks identically for a final result, a silence/start
            // timeout, or Ctrl+C: only Ctrl+C ever cancels the shared token, so this is the sole
            // reliable way to distinguish a genuine cancellation from a legitimate empty result.
            if (cancellationToken.IsCancellationRequested)
            {
                context.WriteError(SpeechCanceledMessage);
                return (recognizedText, true);
            }

            return (recognizedText, false);
        }
        finally
        {
            onRecognizerCreated(null);
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

                case TtsParamFlag:
                    var ttsToken = RequireValue(args, ref index, TtsParamFlag);
                    rawTtsParameters.Add(ParameterBagParser.ParseToken(ttsToken, TtsParamFlag));
                    break;

                case SttParamFlag:
                    var sttToken = RequireValue(args, ref index, SttParamFlag);
                    rawSttParameters.Add(ParameterBagParser.ParseToken(sttToken, SttParamFlag));
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
    /// <param name="SilenceTimeoutSeconds">
    ///     The idle timeout, in seconds, supplied via <c>--silence-timeout</c>, or
    ///     <see langword="null"/> to use the 5-second default (see <c>DefaultSilenceTimeoutSeconds</c>).
    /// </param>
    /// <param name="StartTimeoutSeconds">
    ///     The idle timeout, in seconds, used only before the first recognition result arrives,
    ///     supplied via <c>--start-timeout</c>, or <see langword="null"/> to use the 8-second
    ///     default (see <c>DefaultStartTimeoutSeconds</c>).
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
