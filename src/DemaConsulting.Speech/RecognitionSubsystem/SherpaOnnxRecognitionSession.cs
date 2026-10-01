using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Real <see cref="IRecognitionSession"/> implementation that streams one capture device's
///     audio through a resampler into a shared, "hot" recognition backend for the life of exactly
///     one run.
/// </summary>
/// <remarks>
///     This type carries the per-session pump-loop logic that
///     <c>SherpaOnnxSpeechRecognizer</c> used to own directly before the Engine/Session split: the
///     capture device's <c>FrameCaptured</c> callback still does nothing but copy a block into a
///     bounded, drop-oldest <see cref="Channel{T}"/>, and a single dedicated background thread -
///     started via the subsystem's internal <see cref="DedicatedWorker"/> - still does all the
///     real work (downmix/resample, feed the backend, poll for results, buffer them for
///     <see cref="GetResultsAsync"/>). The backend itself is owned and disposed by the owning
///     <see cref="SherpaOnnxSpeechRecognizerEngine"/>, not by this session, so that it stays
///     "hot" (loaded once, reused across sequential sessions) rather than being torn down at the
///     end of every run.
///     <para>
///     <see cref="StopAsync"/> completes the pending-frame channel and awaits the pump thread, so
///     every block accepted before the call has been decoded and every resulting event buffered
///     by the time it returns - including one last flushed result for a trailing utterance that
///     had not yet been decoded (see <see cref="IRecognitionBackend.TryFlush"/>). Both
///     <see cref="StopAsync"/> and <see cref="DisposeAsync"/> await that pump thread through the
///     cooperative-cancel-then-abandon policy documented on <see cref="DedicatedWorker"/>
///     (Decision #4): an abandoned stop/dispose still completes teardown as best-effort, while an
///     abandonment that happens for any other reason transitions this session to
///     <see cref="RecognitionSessionState.Faulted"/>.
///     </para>
/// </remarks>
internal sealed class SherpaOnnxRecognitionSession : IRecognitionSession
{
    /// <summary>
    ///     The maximum number of captured blocks held pending recognition before the oldest is
    ///     dropped. Sized for roughly a second of typical capture-callback blocks, mirroring the
    ///     bound <c>SherpaOnnxSpeechRecognizer</c> used before this split.
    /// </summary>
    private const int PendingFrameCapacity = 64;

    /// <summary>
    ///     The maximum number of results drained from the backend for a single captured block, so
    ///     that a faulty backend which always reports a result cannot livelock the pump thread.
    /// </summary>
    private const int MaxResultsPerFrame = 32;

    /// <summary>The diagnostics category used for every event this session reports.</summary>
    private const string DiagnosticsCategory = "RecognitionSubsystem";

    /// <summary>Guards every state transition and the fields touched by Start/Stop/Dispose.</summary>
    private readonly object _syncRoot = new();

    /// <summary>The recognition backend this session feeds; owned and disposed by the engine, not this session.</summary>
    private readonly IRecognitionBackend _backend;

    /// <summary>The capture device supplying the audio to recognize, bound for this session's entire life.</summary>
    private readonly IAudioCaptureDevice _device;

    /// <summary>The recognition model whose <see cref="IRecognitionModel.NormalizeText(string,bool)"/> hook is applied to every result.</summary>
    private readonly IRecognitionModel _model;

    /// <summary>The conversion from the device's capture format to the backend's required format.</summary>
    private readonly AudioFrameResampler _resampler;

    /// <summary>The sink for structural lifecycle and fault events.</summary>
    private readonly ISpeechDiagnostics _diagnostics;

    /// <summary>Runs the pump loop on a dedicated, non-pooled thread with a cooperative-cancel-then-abandon policy.</summary>
    private readonly DedicatedWorker _worker;

    /// <summary>Invoked exactly once, at the very end of <see cref="DisposeAsync"/>, to release the engine's exclusivity lease.</summary>
    private readonly Action _releaseLease;

    /// <summary>Buffers provisional/final results for <see cref="GetResultsAsync"/> (Decision #5).</summary>
    private readonly RecognitionResultBuffer _resultBuffer;

    /// <summary>This session's current lifecycle state.</summary>
    private RecognitionSessionState _state = RecognitionSessionState.Created;

    /// <summary>The bounded hand-off between the audio callback thread and the pump thread.</summary>
    private Channel<float[]>? _pendingFrames;

    /// <summary>Signals the pump thread to stop cooperatively and bounds how long teardown waits for it.</summary>
    private CancellationTokenSource? _pumpCts;

    /// <summary>The task returned by <see cref="DedicatedWorker.RunAsync"/> for the running pump loop.</summary>
    private Task? _pumpTask;

    /// <summary>1 while a <see cref="GetResultsAsync"/> enumeration is active; enforces the single-consumer contract.</summary>
    private int _consuming;

    /// <summary>Whether <see cref="DisposeAsync"/> has already run to completion.</summary>
    private bool _isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SherpaOnnxRecognitionSession"/> class,
    ///     bound to an already-loaded backend and an available capture device.
    /// </summary>
    /// <param name="backend">The shared, "hot" recognition backend this session feeds. Must not be null.</param>
    /// <param name="device">The available capture device to stream audio from. Must not be null.</param>
    /// <param name="targetSampleRate">The rate, in Hz, the backend requires its input at. Must be greater than zero.</param>
    /// <param name="model">
    ///     The recognition model owning the backend, whose
    ///     <see cref="IRecognitionModel.NormalizeText(string,bool)"/> hook is applied to every
    ///     result before it is buffered. Must not be null.
    /// </param>
    /// <param name="releaseLease">
    ///     Invoked exactly once, at the very end of <see cref="DisposeAsync"/>, to release the
    ///     owning engine's exclusivity lease. Must not be null.
    /// </param>
    /// <param name="diagnostics">The sink for structural lifecycle and fault events. Must not be null.</param>
    /// <param name="worker">The dedicated worker this session runs its pump loop through. Must not be null.</param>
    internal SherpaOnnxRecognitionSession(
        IRecognitionBackend backend,
        IAudioCaptureDevice device,
        int targetSampleRate,
        IRecognitionModel model,
        Action releaseLease,
        ISpeechDiagnostics diagnostics,
        DedicatedWorker worker)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(releaseLease);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetSampleRate, 0);

        _backend = backend;
        _device = device;
        _model = model;
        _releaseLease = releaseLease;
        _diagnostics = diagnostics;
        _worker = worker;
        _resultBuffer = new RecognitionResultBuffer(diagnostics, DiagnosticsCategory);

        var deviceSampleRate = device.SampleRate;
        var deviceChannelCount = device.ChannelCount;
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

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <see langword="true"/>: this type is only ever created by
    ///     <see cref="SherpaOnnxSpeechRecognizerEngine"/> for a real, available device.
    /// </remarks>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public RecognitionSessionState State
    {
        get
        {
            lock (_syncRoot)
            {
                return _state;
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler<SessionStateChangedEventArgs>? StateChanged;

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (_state != RecognitionSessionState.Created)
            {
                throw new InvalidOperationException(
                    "Cannot start recognition: this session has already been started, stopped, or disposed. " +
                    "Sessions are single-use; create a new session via ISpeechRecognizerEngine.CreateSessionAsync to run again.");
            }

            TransitionTo(RecognitionSessionState.Starting);
        }

        var frames = Channel.CreateBounded<float[]>(
            new BoundedChannelOptions(PendingFrameCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
        _pendingFrames = frames;

        _device.FrameCaptured += OnFrameCaptured;
        try
        {
            _device.Start();
        }
        catch (Exception ex)
        {
            _device.FrameCaptured -= OnFrameCaptured;
            _pendingFrames = null;

            lock (_syncRoot)
            {
                TransitionTo(RecognitionSessionState.Faulted);
            }

            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"Failed to start recognition because the capture device could not start: {ex.Message}");
            _resultBuffer.Fault(ex);
            throw new SpeechRecognizerUnavailableException(
                "Cannot start recognition: the capture device failed to start.",
                ex);
        }

        _pumpCts = new CancellationTokenSource();
        _pumpTask = _worker.RunAsync(PumpLoop, _pumpCts.Token);

        lock (_syncRoot)
        {
            TransitionTo(RecognitionSessionState.Running);
        }

        _diagnostics.Report(SpeechDiagnosticLevel.Info, DiagnosticsCategory, "Started streaming recognition.");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? pumpTask;
        CancellationTokenSource? pumpCts;
        lock (_syncRoot)
        {
            switch (_state)
            {
                case RecognitionSessionState.Created:
                    // Never started; nothing to tear down, but single-use still applies.
                    TransitionTo(RecognitionSessionState.Stopped);
                    return;
                case RecognitionSessionState.Stopped:
                case RecognitionSessionState.Disposing:
                case RecognitionSessionState.Disposed:
                case RecognitionSessionState.Faulted:
                    // Idempotent: already converged on a terminal-for-Stop state.
                    return;
                case RecognitionSessionState.Stopping:
                    // Another caller is already stopping; fall through and await the same pump
                    // task so every caller's returned Task completes once that happens.
                    break;
                default:
                    TransitionTo(RecognitionSessionState.Stopping);
                    break;
            }

            pumpTask = _pumpTask;
            pumpCts = _pumpCts;
        }

        _device.FrameCaptured -= OnFrameCaptured;
        _pendingFrames?.Writer.TryComplete();

        // Cancelling here is purely the abandon-timeout deadline for DedicatedWorker (Decision
        // #4): PumpLoop itself never observes this token while draining (see its own remarks), so
        // every block already accepted is still decoded and buffered normally; this cancellation
        // only matters if the pump thread is genuinely stuck inside a blocking backend call that
        // never returns, in which case the worker is abandoned after its timeout rather than
        // hanging this call forever.
        if (pumpCts is not null)
        {
            await pumpCts.CancelAsync().ConfigureAwait(false);
        }

        if (pumpTask is not null)
        {
            try
            {
                await pumpTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // An abandoned pump thread during an explicit Stop completes teardown
                // best-effort rather than faulting the caller (Decision #4).
            }
            catch (Exception ex)
            {
                _diagnostics.Report(
                    SpeechDiagnosticLevel.Error,
                    DiagnosticsCategory,
                    $"The recognition pump loop ended with a fault: {ex.Message}");
            }
        }

        pumpCts?.Dispose();
        _resultBuffer.Complete();

        try
        {
            _backend.Reset();
        }
        catch (Exception ex)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"Failed to reset the recognition backend after stopping: {ex.Message}");
        }

        try
        {
            _device.Stop();
        }
        catch (Exception ex)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"Failed to stop the capture device after recognition: {ex.Message}");
        }

        lock (_syncRoot)
        {
            if (_state == RecognitionSessionState.Stopping)
            {
                TransitionTo(RecognitionSessionState.Stopped);
            }
        }

        _diagnostics.Report(SpeechDiagnosticLevel.Info, DiagnosticsCategory, "Stopped streaming recognition.");
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<SpeechRecognitionEvent> GetResultsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _consuming, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "GetResultsAsync is single-consumer: a previous enumeration of this session's results is still active.");
        }

        try
        {
            await foreach (var result in _resultBuffer.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return result;
            }
        }
        finally
        {
            Volatile.Write(ref _consuming, 0);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
        }

        // Runs the full Stopping -> Stopped teardown (device/pump/backend) first, matching the
        // documented forward-only state machine exactly: a session disposed directly from
        // Running still visits Stopping/Stopped before Disposing/Disposed, rather than skipping
        // that teardown by jumping straight to Disposing.
        await StopAsync(CancellationToken.None).ConfigureAwait(false);

        lock (_syncRoot)
        {
            TransitionTo(RecognitionSessionState.Disposing);
            TransitionTo(RecognitionSessionState.Disposed);
        }

        _releaseLease();
    }

    /// <summary>
    ///     Moves this session to <paramref name="newState"/> and raises
    ///     <see cref="StateChanged"/>. Must be called under <see cref="_syncRoot"/>.
    /// </summary>
    private void TransitionTo(RecognitionSessionState newState)
    {
        var previous = _state;
        _state = newState;

        try
        {
            StateChanged?.Invoke(this, new SessionStateChangedEventArgs(previous, newState));
        }
        catch (Exception ex)
        {
            // Intentionally broad: a host's StateChanged handler must never be able to break this
            // session's own state transition, mirroring ResultReceived's documented convention.
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"A StateChanged handler threw an exception: {ex.Message}");
        }
    }

    /// <summary>
    ///     Enqueues one captured block for recognition. Runs on the capture device's audio
    ///     callback thread and therefore does no work beyond copying and queueing, and checks
    ///     whether the device has gone unavailable mid-session.
    /// </summary>
    private void OnFrameCaptured(object? sender, AudioCaptureFrameEventArgs e)
    {
        try
        {
            if (!_device.IsAvailable)
            {
                FaultSession(new SpeechRecognizerUnavailableException(
                    "The capture device became unavailable while a recognition session was running."));
                return;
            }

            var frames = _pendingFrames;
            if (frames is null || e.Samples.Count == 0)
            {
                return;
            }

            frames.Writer.TryWrite([.. e.Samples]);
        }
        catch (Exception ex)
        {
            // Intentionally broad: this event runs on the native audio callback thread, and no
            // exception may be allowed to escape back into that callback.
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"A captured audio block could not be queued for recognition: {ex.Message}");
        }
    }

    /// <summary>
    ///     Transitions this session to <see cref="RecognitionSessionState.Faulted"/> and surfaces
    ///     <paramref name="cause"/> through any active <see cref="GetResultsAsync"/> enumeration.
    /// </summary>
    private void FaultSession(Exception cause)
    {
        lock (_syncRoot)
        {
            if (_state is RecognitionSessionState.Starting or RecognitionSessionState.Running or RecognitionSessionState.Stopping)
            {
                TransitionTo(RecognitionSessionState.Faulted);
            }
        }

        _resultBuffer.Fault(cause);
        _pumpCts?.Cancel();
    }

    /// <summary>
    ///     Drains queued capture blocks through the recognition pipeline until the queue is
    ///     completed, then flushes any trailing audio. Runs synchronously on the dedicated pump
    ///     thread (see <see cref="DedicatedWorker"/>).
    /// </summary>
    /// <remarks>
    ///     Deliberately does not observe the <see cref="DedicatedWorker"/> cancellation token
    ///     passed to <see cref="DedicatedWorker.RunAsync"/>: that token is cancelled by
    ///     <see cref="StopAsync"/> purely as the outer abandon-timeout deadline (Decision #4), and
    ///     reading it here as well would risk silently dropping buffered-but-undecoded frames in
    ///     a race with that cancellation, breaking the guarantee that every block accepted before
    ///     <see cref="StopAsync"/> is still decoded. Completion of <see cref="_pendingFrames"/>'s
    ///     writer is the only signal this loop reacts to.
    /// </remarks>
    private void PumpLoop(CancellationToken _)
    {
        var reader = _pendingFrames!.Reader;
        while (true)
        {
            float[] frame;
            try
            {
                frame = reader.ReadAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
            }
            catch (ChannelClosedException)
            {
                break;
            }

            ProcessFrame(frame);
        }

        FlushFinal();
    }

    /// <summary>
    ///     Finalizes and buffers any trailing audio the backend has accepted but not yet decoded,
    ///     as the very last action of the pump loop.
    /// </summary>
    private void FlushFinal()
    {
        try
        {
            if (!_backend.TryFlush(out var result) || result is null)
            {
                return;
            }

            BufferResult(result);
        }
        catch (Exception ex)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"Failed to flush the recognition backend's trailing audio during teardown: {ex.Message}");
        }
    }

    /// <summary>
    ///     Converts one captured block to the backend's format, feeds it in, and buffers every
    ///     result it produced.
    /// </summary>
    private void ProcessFrame(float[] interleavedSamples)
    {
        try
        {
            var monoSamples = _resampler.Convert(interleavedSamples);
            _backend.AcceptSamples(monoSamples);

            for (var i = 0; i < MaxResultsPerFrame; i++)
            {
                if (!_backend.TryDecode(out var result) || result is null)
                {
                    return;
                }

                BufferResult(result);
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

    /// <summary>
    ///     Applies the owning model's text restoration and adds the result to
    ///     <see cref="_resultBuffer"/>.
    /// </summary>
    private void BufferResult(SpeechRecognitionResult result)
    {
        var restoredText = _model.NormalizeText(result.Text, result.IsFinal);
        var restoredResult = restoredText == result.Text ? result : result with { Text = restoredText };
        _resultBuffer.AddResult(new SpeechRecognitionEvent(restoredResult));
    }
}
