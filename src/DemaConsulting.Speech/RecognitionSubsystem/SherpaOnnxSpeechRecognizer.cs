using System.Threading.Channels;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Real <see cref="ISpeechRecognizer"/> implementation that streams a capture device's audio
///     through a resampler into a recognition engine and raises the resulting provisional and
///     final results.
/// </summary>
/// <remarks>
///     The pipeline is deliberately split across two threads. The capture device raises
///     <c>FrameCaptured</c> from a high-priority audio callback thread where blocking work would
///     cause dropouts, so the frame handler does nothing but copy the block into a bounded
///     channel and return. A single background consumer task then does all the real work -
///     downmix/resample, feed the engine, poll for results, and raise
///     <see cref="ResultReceived"/> - so no recognition cost is ever paid on the audio thread and
///     handlers never run on it.
///     <para>
///     The channel is bounded and drops the oldest queued block when full. Recognition that has
///     fallen behind live audio can never be caught up by queueing more of it, so bounding the
///     backlog keeps memory flat and latency honest instead of growing both without limit.
///     </para>
///     <para>
///     <see cref="Stop"/> completes the channel and waits for the consumer to finish, so every
///     block accepted before the call has been decoded and every resulting event raised by the
///     time it returns. That makes the pipeline deterministic for both hosts and tests, with no
///     polling or timing assumptions anywhere.
///     </para>
///     <para>
///     Exceptions raised anywhere in the pipeline - by a frame handler, by the engine, or by a
///     host's own <see cref="ResultReceived"/> handler - are caught and reported through the
///     diagnostics sink. They are never rethrown into the PortAudio callback, where an escaping
///     exception would tear down the audio stream, and never stop the recognizer.
///     </para>
///     <para>
///     Every decoded result is passed through the owning <see cref="IRecognitionModel"/>'s
///     <see cref="IRecognitionModel.NormalizeText(string,bool)"/> hook before
///     <see cref="ResultReceived"/> is raised, so per-model text restoration (e.g. casing/
///     contraction/punctuation restoration for a model whose raw output "yells") is applied
///     uniformly regardless of which model is in use - a model that already produces properly
///     cased/punctuated output relies on the hook's identity default and pays no cost beyond one
///     delegating call.
///     </para>
/// </remarks>
internal sealed class SherpaOnnxSpeechRecognizer : ISpeechRecognizer
{
    /// <summary>
    ///     The maximum number of captured blocks held pending recognition before the oldest is
    ///     dropped. Sized for roughly a second of typical capture-callback blocks: long enough to
    ///     ride out a decoding hiccup, short enough that a persistently overloaded machine sheds
    ///     audio instead of accumulating unbounded latency.
    /// </summary>
    private const int PendingFrameCapacity = 64;

    /// <summary>
    ///     The maximum number of results drained from the engine for a single captured block.
    ///     A correct engine reports at most a partial and a final per block; this bound exists so
    ///     that a faulty engine which always reports a result cannot livelock the consumer.
    /// </summary>
    private const int MaxResultsPerFrame = 32;

    /// <summary>The diagnostics category used for every event this recognizer reports.</summary>
    private const string DiagnosticsCategory = "RecognitionSubsystem";

    /// <summary>
    ///     Initializes a new instance of the <see cref="SherpaOnnxSpeechRecognizer"/> class over
    ///     an already-loaded engine and an available capture device.
    /// </summary>
    /// <param name="engine">
    ///     The loaded recognition engine this recognizer owns and disposes. Must not be null.
    /// </param>
    /// <param name="captureDevice">
    ///     The available capture device to stream audio from. Must not be null and must report
    ///     <c>IsAvailable</c> as <see langword="true"/>; <see cref="SpeechRecognizerFactory"/>
    ///     guarantees both.
    /// </param>
    /// <param name="targetSampleRate">
    ///     The rate, in Hz, the engine requires its input at. Must be greater than zero.
    /// </param>
    /// <param name="model">
    ///     The recognition model owning this engine, whose
    ///     <see cref="IRecognitionModel.NormalizeText(string,bool)"/> hook is applied to every
    ///     result before it is raised. Must not be null. Required (not optional), mirroring
    ///     <c>SherpaOnnxSpeechSynthesizer</c>'s existing required <c>ISynthesisModel</c>
    ///     constructor parameter.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink for structural lifecycle and fault events, or <see langword="null"/> to use
    ///     <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="engine"/>, <paramref name="captureDevice"/>, or
    ///     <paramref name="model"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="targetSampleRate"/> is less than or equal to zero.
    /// </exception>
    /// <remarks>
    ///     Construction never starts capture and never touches hardware; it only records the
    ///     conversion the running pipeline will need.
    /// </remarks>
    internal SherpaOnnxSpeechRecognizer(
        IRecognitionEngine engine,
        IAudioCaptureDevice captureDevice,
        int targetSampleRate,
        IRecognitionModel model,
        ISpeechDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(captureDevice);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetSampleRate, 0);

        _engine = engine;
        _captureDevice = captureDevice;
        _model = model;
        _diagnostics = diagnostics ?? NullSpeechDiagnostics.Instance;

        // Resolve the device's actual capture format now. A device that reports a non-positive
        // rate or channel count cannot be honored, so fall back to a pass-through conversion and
        // say so, rather than throwing at composition time or silently corrupting the audio.
        var deviceSampleRate = captureDevice.SampleRate;
        var deviceChannelCount = captureDevice.ChannelCount;
        if (deviceSampleRate <= 0 || deviceChannelCount <= 0)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                "Capture device reported an unusable audio format; assuming mono audio already at the model's rate.");
            deviceSampleRate = targetSampleRate;
            deviceChannelCount = 1;
        }

        _resampler = new AudioFrameResampler(deviceSampleRate, deviceChannelCount, targetSampleRate);
    }

    /// <summary>Guards the running-state transitions performed by Start, Stop, and Dispose.</summary>
    private readonly object _syncRoot = new();

    /// <summary>The recognition engine this recognizer owns, uses, and disposes.</summary>
    private readonly IRecognitionEngine _engine;

    /// <summary>The capture device supplying the audio to recognize.</summary>
    private readonly IAudioCaptureDevice _captureDevice;

    /// <summary>The recognition model whose <see cref="IRecognitionModel.NormalizeText(string,bool)"/> hook is applied to every result.</summary>
    private readonly IRecognitionModel _model;

    /// <summary>The conversion from the device's capture format to the engine's required format.</summary>
    private readonly AudioFrameResampler _resampler;

    /// <summary>The sink for structural lifecycle and fault events.</summary>
    private readonly ISpeechDiagnostics _diagnostics;

    /// <summary>The bounded hand-off between the audio callback thread and the consumer task.</summary>
    private Channel<float[]>? _pendingFrames;

    /// <summary>The background task draining <see cref="_pendingFrames"/> while running.</summary>
    private Task? _consumerTask;

    /// <summary>Whether capture is currently subscribed and the consumer task is running.</summary>
    private bool _isRunning;

    /// <summary>Whether <see cref="Dispose"/> has already run.</summary>
    private bool _isDisposed;

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <see langword="true"/>: this type is only ever created by
    ///     <see cref="SpeechRecognizerFactory"/> after the engine loaded successfully and the
    ///     capture device reported itself available, so its existence is itself the availability
    ///     guarantee. Every unavailable case is represented by
    ///     <see cref="UnavailableSpeechRecognizer"/> instead.
    /// </remarks>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public event EventHandler<SpeechRecognitionEvent>? ResultReceived;

    /// <inheritdoc/>
    public void Start()
    {
        Channel<float[]> frames;
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            // Starting an already-running recognizer is a no-op rather than an error, matching
            // PortAudioCaptureDevice.Start and letting a host call it defensively.
            if (_isRunning)
            {
                return;
            }

            // Drop the oldest pending block when the consumer falls behind: stale audio is worth
            // less than bounded memory and bounded latency.
            frames = Channel.CreateBounded<float[]>(
                new BoundedChannelOptions(PendingFrameCapacity)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = false
                });
            _pendingFrames = frames;
            _consumerTask = Task.Run(() => ConsumeAsync(frames.Reader));
            _isRunning = true;
        }

        // Subscribe before starting so no captured block can be raised before there is a handler
        // to enqueue it.
        _captureDevice.FrameCaptured += OnFrameCaptured;
        try
        {
            _captureDevice.Start();
        }
        catch (Exception ex)
        {
            // The device claimed to be available but failed on first use. Unwind everything this
            // call set up so a later retry starts from a clean state, then surface the failure.
            _captureDevice.FrameCaptured -= OnFrameCaptured;
            Task? consumerToDrain;
            lock (_syncRoot)
            {
                _isRunning = false;
                _pendingFrames = null;
                consumerToDrain = _consumerTask;
                _consumerTask = null;
            }

            frames.Writer.TryComplete();
            WaitForConsumer(consumerToDrain);

            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"Failed to start recognition because the capture device could not start: {ex.Message}");
            throw new SpeechRecognizerUnavailableException(
                "Cannot start recognition: the capture device failed to start.",
                ex);
        }

        _diagnostics.Report(SpeechDiagnosticLevel.Info, DiagnosticsCategory, "Started streaming recognition.");
    }

    /// <inheritdoc/>
    public void Stop()
    {
        StopCore(reportStopped: true);
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Stops the pipeline (if running) and disposes the owned engine. Idempotent: a second
    ///     call does nothing, so a host may safely dispose a recognizer it has already disposed.
    /// </remarks>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
        }

        StopCore(reportStopped: false);
        _engine.Dispose();
    }

    /// <summary>
    ///     Performs the shared stop sequence: unsubscribe, complete the queue, drain the
    ///     consumer, and stop the capture device.
    /// </summary>
    /// <param name="reportStopped">
    ///     Whether to report a structural "stopped" event. Suppressed during disposal, where the
    ///     stop is an implementation detail of tearing the object down rather than a lifecycle
    ///     transition a host asked for.
    /// </param>
    /// <remarks>
    ///     The running state is captured under the lock but the consumer is awaited outside it,
    ///     so a <see cref="ResultReceived"/> handler that calls back into the recognizer from the
    ///     consumer thread cannot deadlock against a concurrent stop.
    /// </remarks>
    private void StopCore(bool reportStopped)
    {
        Channel<float[]>? frames;
        Task? consumerTask;
        lock (_syncRoot)
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            frames = _pendingFrames;
            consumerTask = _consumerTask;
            _pendingFrames = null;
            _consumerTask = null;
        }

        // Detach first so no further blocks are queued, then let the consumer drain what is
        // already queued and exit.
        _captureDevice.FrameCaptured -= OnFrameCaptured;
        frames?.Writer.TryComplete();
        WaitForConsumer(consumerTask);

        try
        {
            _captureDevice.Stop();
        }
        catch (Exception ex)
        {
            // Stopping the device is best-effort during teardown: the recognizer is already
            // detached, so a device fault here must not prevent Stop/Dispose from completing.
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"Failed to stop the capture device after recognition: {ex.Message}");
        }

        if (reportStopped)
        {
            _diagnostics.Report(SpeechDiagnosticLevel.Info, DiagnosticsCategory, "Stopped streaming recognition.");
        }
    }

    /// <summary>
    ///     Blocks until the background consumer task has finished draining, reporting rather than
    ///     propagating any failure it ended with.
    /// </summary>
    /// <param name="consumerTask">The consumer task to await, or <see langword="null"/> when none was started.</param>
    private void WaitForConsumer(Task? consumerTask)
    {
        if (consumerTask is null)
        {
            return;
        }

        try
        {
            consumerTask.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"The recognition consumer loop ended with a fault: {ex.Message}");
        }
    }

    /// <summary>
    ///     Enqueues one captured block for recognition. Runs on the capture device's audio
    ///     callback thread and therefore does no work beyond copying and queueing.
    /// </summary>
    /// <param name="sender">The capture device raising the event; unused.</param>
    /// <param name="e">The captured block of interleaved samples.</param>
    /// <remarks>
    ///     Every failure is swallowed and reported: an exception escaping this handler would
    ///     propagate into the native PortAudio callback and tear down the audio stream.
    /// </remarks>
    private void OnFrameCaptured(object? sender, AudioCaptureFrameEventArgs e)
    {
        try
        {
            // Re-read the field once: a concurrent Stop may have cleared it between the event
            // being raised and this handler running.
            var frames = _pendingFrames;
            if (frames is null || e.Samples.Count == 0)
            {
                return;
            }

            frames.Writer.TryWrite([.. e.Samples]);
        }
        catch (Exception ex)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"A captured audio block could not be queued for recognition: {ex.Message}");
        }
    }

    /// <summary>
    ///     Drains queued capture blocks through the recognition pipeline until the queue is
    ///     completed and empty.
    /// </summary>
    /// <param name="reader">The queue to drain.</param>
    /// <returns>A task that completes once the queue is completed and fully drained.</returns>
    private async Task ConsumeAsync(ChannelReader<float[]> reader)
    {
        await foreach (var frame in reader.ReadAllAsync().ConfigureAwait(false))
        {
            ProcessFrame(frame);
        }
    }

    /// <summary>
    ///     Converts one captured block to the engine's format, feeds it in, and raises every
    ///     result it produced.
    /// </summary>
    /// <param name="interleavedSamples">The captured block, interleaved by channel.</param>
    /// <remarks>
    ///     All failures are contained here so that one bad block, one engine fault, or one
    ///     throwing host handler cannot end the session.
    /// </remarks>
    private void ProcessFrame(float[] interleavedSamples)
    {
        try
        {
            // Convert the device's native format into the single format the model accepts.
            var monoSamples = _resampler.Convert(interleavedSamples);
            _engine.AcceptSamples(monoSamples);

            // Drain every result this block produced, bounded so a misbehaving engine cannot
            // livelock the consumer.
            for (var i = 0; i < MaxResultsPerFrame; i++)
            {
                if (!_engine.TryDecode(out var result) || result is null)
                {
                    return;
                }

                // Apply the owning model's own text restoration (e.g. casing/contraction/
                // punctuation restoration for a model whose raw output "yells") before the
                // result reaches any consumer, mirroring SherpaOnnxSpeechSynthesizer's own
                // NormalizeText call on the synthesis side.
                var restoredText = _model.NormalizeText(result.Text, result.IsFinal);
                var restoredResult = restoredText == result.Text ? result : result with { Text = restoredText };

                ResultReceived?.Invoke(this, new SpeechRecognitionEvent(restoredResult));
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"A captured audio block could not be recognized: {ex.Message}");
        }
    }
}
