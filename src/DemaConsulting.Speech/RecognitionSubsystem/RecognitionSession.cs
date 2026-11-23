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
///     capture device's <c>FrameCaptured</c> callback does nothing but copy a block into a
///     <see cref="Channel{T}"/>, and a single dedicated background thread - started via the
///     subsystem's internal <see cref="DedicatedWorker"/> - does all the real work
///     (downmix/resample, feed the backend, poll for results, buffer them for
///     <see cref="GetResultsAsync"/>). The backend itself is owned and disposed by the owning
///     <see cref="SpeechRecognizerEngine"/>, not by this session, so that it stays
///     "hot" (loaded once, reused across sequential sessions) rather than being torn down at the
///     end of every run.
///     <para>
///     <b>Lossless capture queue.</b> A live device's audio callback must never block, so a
///     live source queues into an unbounded channel: a recognizer that falls behind builds a
///     backlog rather than losing audio. Memory is still bounded by a safety cap of
///     <see cref="DefaultMaxPendingLiveAudio"/> of queued audio; a block that would exceed the
///     cap is discarded and reported as a warning diagnostic (never silently). A finite file
///     source is delivered on its own reader thread, so it instead queues into a small bounded
///     channel that applies backpressure to the reader.
///     </para>
///     <para>
///     <b>Graceful stop.</b> <see cref="StopAsync"/> (and <see cref="DisposeAsync"/>) stops
///     capture by unsubscribing from the device, completes the pending-frame channel, and awaits
///     the pump thread, so every block accepted before the call has been decoded, the backend has
///     been flushed (see <see cref="IRecognitionBackend.TryFlush"/>), and every resulting event
///     has been buffered by the time it returns. Only then is the backend reset, so the next
///     session on the same engine starts a clean utterance. A slow drain is never cut short: the
///     cooperative-cancel-then-abandon policy documented on <see cref="DedicatedWorker"/> is
///     applied only once the pump stops making progress for a whole abandon interval (a
///     genuinely stuck native call). An abandoned stop/dispose still completes teardown as
///     best-effort, while an abandonment that happens for any other reason transitions this
///     session to <see cref="RecognitionSessionState.Faulted"/>.
///     </para>
///     <para>
///     <b>Canceled stop.</b> Canceling the token passed to <see cref="StopAsync"/> is the only
///     way to discard audio: the pump stops decoding blocks still queued, skips the final flush,
///     and the abandon policy applies immediately. Teardown (backend reset, device stop, state
///     convergence) still completes for every caller.
///     </para>
/// </remarks>
internal sealed class RecognitionSession : IRecognitionSession
{
    /// <summary>
    ///     The default safety cap on how much live audio may wait for recognition: one minute.
    /// </summary>
    /// <remarks>
    ///     A recognizer this far behind real time is no longer producing useful live results, so
    ///     the cap only exists to keep memory bounded (one minute of 48 kHz stereo audio is about
    ///     23 MB) if a recognizer is persistently slower than real time. Ordinary backlogs - a
    ///     slow first decode, a busy machine, a long stop drain - are far below it.
    /// </remarks>
    internal static readonly TimeSpan DefaultMaxPendingLiveAudio = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     The maximum number of blocks a finite file source may queue before its reader thread
    ///     waits for the pump to catch up (backpressure, never dropping).
    /// </summary>
    private const int FiniteSourcePendingFrameCapacity = 64;

    /// <summary>
    ///     The maximum number of results drained from the backend for a single captured block, so
    ///     that a faulty backend which always reports a result cannot livelock the pump thread.
    /// </summary>
    private const int MaxResultsPerFrame = 32;

    /// <summary>
    ///     The maximum number of results drained from the backend at teardown, before the final
    ///     flush, so that results left over by the per-block cap are still delivered while a
    ///     faulty backend that always reports a result still cannot livelock the pump thread.
    /// </summary>
    private const int MaxResultsAtFinalDrain = 1024;

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

    /// <summary>Buffers provisional/final results for <see cref="GetResultsAsync"/>.</summary>
    private readonly RecognitionResultBuffer _resultBuffer;

    /// <summary>
    ///     Whether <see cref="_device"/> is a finite file source, which queues with backpressure
    ///     and drains without an abandon deadline, rather than a live device.
    /// </summary>
    private readonly bool _isFiniteSource;

    /// <summary>
    ///     The live-source safety cap, in interleaved samples at the device's capture format:
    ///     a captured block that would grow the queued backlog beyond this is discarded and
    ///     reported (see <see cref="DefaultMaxPendingLiveAudio"/>).
    /// </summary>
    private readonly long _maxPendingLiveSamples;

    /// <summary>
    ///     The number of live interleaved samples currently queued but not yet read by the pump.
    ///     Updated with <see cref="Interlocked"/> by the capture callback and the pump thread.
    /// </summary>
    private long _pendingLiveSamples;

    /// <summary>
    ///     The number of live interleaved samples discarded because the safety cap was reached,
    ///     reported once more at teardown. Updated with <see cref="Interlocked"/>.
    /// </summary>
    private long _droppedLiveSamples;

    /// <summary>
    ///     1 once the first overflow of the live safety cap has been reported, so a sustained
    ///     overflow produces one warning rather than one per captured block.
    /// </summary>
    private int _overflowReported;

    /// <summary>
    ///     Incremented by the pump thread after every completed backend step (one block, one
    ///     final-drain decode, the flush), so teardown can tell a slow-but-progressing drain -
    ///     which it waits for - from a stuck native call - which it abandons.
    /// </summary>
    private long _pumpProgress;

    /// <summary>
    ///     Set once a caller cancels <see cref="StopAsync"/>: the pump then discards every block
    ///     still queued instead of decoding it, and skips the final flush. Cancellation is the
    ///     only path that may discard captured audio.
    /// </summary>
    private volatile bool _discardRequested;

    /// <summary>
    ///     1 while the session accepts captured blocks (from start until a stop or fault is
    ///     accepted), otherwise 0. Written with <see cref="Interlocked"/> so, together with
    ///     <see cref="_callbacksInFlight"/>, it forms the barrier described on
    ///     <see cref="OnFrameCaptured"/>.
    /// </summary>
    private int _acceptingFrames;

    /// <summary>
    ///     The number of capture callbacks currently executing <see cref="OnFrameCaptured"/>.
    ///     Teardown of a live source waits for this to reach zero before completing the queue,
    ///     so a block whose callback was already running when the stop was accepted is still
    ///     queued and recognized rather than lost.
    /// </summary>
    private int _callbacksInFlight;
    /// <summary>This session's current lifecycle state.</summary>
    private RecognitionSessionState _state = RecognitionSessionState.Created;

    /// <summary>The hand-off between the audio callback thread and the pump thread (see the type remarks).</summary>
    private Channel<float[]>? _pendingFrames;

    /// <summary>Signals the pump thread to stop cooperatively and bounds how long teardown waits for it.</summary>
    private CancellationTokenSource? _pumpCts;

    /// <summary>The abandon-aware task returned by <see cref="DedicatedWorker.RunAsync(Action{CancellationToken}, CancellationToken)"/> for the running pump loop.</summary>
    private Task? _pumpTask;

    /// <summary>
    ///     The pump loop's own dedicated-thread completion, set alongside <see cref="_pumpTask"/>.
    ///     Unlike <see cref="_pumpTask"/>, this completes only once the pump thread has genuinely
    ///     exited - even if <see cref="_pumpTask"/> itself completed early as abandoned - so
    ///     teardown can safely wait for it before touching the shared backend again.
    /// </summary>
    private Task? _pumpRawCompletion;

    /// <summary>
    ///     The device-start worker's own raw completion, set alongside the abandon-aware task
    ///     <see cref="StartAsync"/> itself awaits - exactly like <see cref="_pumpRawCompletion"/>
    ///     mirrors <see cref="_pumpTask"/>. Set under <see cref="_syncRoot"/> before
    ///     <see cref="StartAsync"/> ever releases the lock, so <see cref="RunTeardownAsync"/> can
    ///     await this - the device's own <see cref="IAudioCaptureDevice.Start"/> call genuinely
    ///     returning, even if <see cref="StartAsync"/>'s own abandon-aware task completed early as
    ///     canceled - before it ever stops the device: this is what still prevents a concurrent
    ///     <see cref="StopAsync"/>/<see cref="DisposeAsync"/> from converging this session to
    ///     <see cref="RecognitionSessionState.Stopped"/> and returning while the device is still
    ///     genuinely starting, now that <see cref="StartAsync"/> no longer blocks the caller for
    ///     the life of that native call (see its own remarks).
    /// </summary>
    private Task? _deviceStartRawCompletion;

    /// <summary>
    ///     Set only when the pump worker was abandoned: the continuation of
    ///     <see cref="_pumpRawCompletion"/> that safely resets the backend and stops the device
    ///     once that raw completion genuinely happens - deferred out of the teardown task itself
    ///     so <see cref="StopAsync"/>/<see cref="FaultSession"/> still complete promptly
    ///     (the abandon guarantee) and this session's own state still converges immediately, while
    ///     <see cref="DisposeCoreAsync"/> still awaits this before releasing the engine's lease,
    ///     so the shared backend is never reset/touched while an abandoned pump thread may still
    ///     be inside it.
    /// </summary>
    private Task? _deferredBackendTeardown;

    /// <summary>
    ///     The single-flight teardown operation (drain the pump, reset the backend, stop the
    ///     device), lazily started by whichever of <see cref="StopAsync"/>, <see cref="FaultSession"/>,
    ///     or <see cref="DisposeAsync"/> first needs it; every other caller awaits this same task
    ///     instead of repeating the (non-idempotent) teardown steps a second time.
    /// </summary>
    private Task? _teardownTask;

    /// <summary>
    ///     The single-flight disposal operation, shared by every concurrent <see cref="DisposeAsync"/>
    ///     caller so a second call awaits the same real teardown rather than returning as soon as
    ///     the first call merely begins.
    /// </summary>
    private Task? _disposeTask;

    /// <summary>1 while a <see cref="GetResultsAsync"/> enumeration is active; enforces the single-consumer contract.</summary>
    private int _consuming;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecognitionSession"/> class,
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
    /// <param name="maxPendingLiveAudio">
    ///     The safety cap on queued live audio, or <see langword="null"/> for
    ///     <see cref="DefaultMaxPendingLiveAudio"/>. Must be positive when supplied; tests inject a
    ///     small value so the cap can be reached cheaply.
    /// </param>
    internal RecognitionSession(
        IRecognitionBackend backend,
        IAudioCaptureDevice device,
        int targetSampleRate,
        IRecognitionModel model,
        Action releaseLease,
        ISpeechDiagnostics diagnostics,
        DedicatedWorker worker,
        TimeSpan? maxPendingLiveAudio = null)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(releaseLease);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetSampleRate, 0);
        var maxPending = maxPendingLiveAudio ?? DefaultMaxPendingLiveAudio;
        if (maxPending <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPendingLiveAudio),
                maxPending,
                "The live-audio safety cap must be positive.");
        }

        _backend = backend;
        _device = device;
        _model = model;
        _releaseLease = releaseLease;
        _diagnostics = diagnostics;
        _worker = worker;
        _resultBuffer = new RecognitionResultBuffer();
        _isFiniteSource = device is WavFileAudioCaptureDevice;

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

        // The cap is expressed in the device's own interleaved samples, which is exactly what the
        // capture callback queues, so it can be enforced without any format conversion there.
        _maxPendingLiveSamples = Math.Max(
            1L,
            (long)(maxPending.TotalSeconds * deviceSampleRate * deviceChannelCount));
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <see langword="true"/>: this type is only ever created by
    ///     <see cref="SpeechRecognizerEngine"/> for a real, available device.
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
    /// <remarks>
    ///     The state transition into <see cref="RecognitionSessionState.Starting"/> - and starting
    ///     the pump thread and subscribing to <see cref="IAudioCaptureDevice.FrameCaptured"/> -
    ///     still happens entirely under <see cref="_syncRoot"/>, but the actual, synchronous and
    ///     potentially slow <see cref="IAudioCaptureDevice.Start"/> call is run through
    ///     <see cref="_worker"/> (the same dedicated-worker idiom <see cref="PumpLoop"/> and
    ///     <see cref="RunTeardownAsync"/> already use) and awaited <em>without</em> holding
    ///     <see cref="_syncRoot"/>, so this method no longer blocks its caller for the life of that
    ///     native call (for example PortAudio's native stream open/start, or a file-backed device
    ///     synchronously replaying an entire file). The call is also given this method's own
    ///     <paramref name="cancellationToken"/>, so the same cooperative-cancel-then-abandon policy
    ///     <see cref="PumpLoop"/> relies on applies here too: a caller that cancels while the device
    ///     is still starting is given <see cref="DedicatedWorker.AbandonTimeout"/> before this call
    ///     gives up waiting and reports cancellation/failure, rather than ignoring the request
    ///     indefinitely. The worker's raw completion - which completes only once
    ///     <see cref="IAudioCaptureDevice.Start"/> genuinely returns, even if abandoned - is
    ///     published to <see cref="_deviceStartRawCompletion"/> before the lock is released, and
    ///     <see cref="RunTeardownAsync"/> always awaits that before it ever stops the device: a
    ///     concurrent <see cref="StopAsync"/> or <see cref="DisposeAsync"/> call can therefore still
    ///     never converge this session to <see cref="RecognitionSessionState.Stopped"/> and return
    ///     while the device is still genuinely starting, even though the two calls no longer
    ///     literally serialize on one lock for the device call's entire duration. Only this call's
    ///     own continuation (once the abandon-aware device-start task completes) transitions this
    ///     session onward from <see cref="RecognitionSessionState.Starting"/> to
    ///     <see cref="RecognitionSessionState.Running"/>/<see cref="RecognitionSessionState.Faulted"/>,
    ///     and only if a concurrent teardown has not already moved this session on first - so a
    ///     torn-down session is never forced back to <see cref="RecognitionSessionState.Running"/>.
    ///     <para>
    ///     The pump is started before <see cref="IAudioCaptureDevice.Start"/>, not after: a
    ///     synchronous-replay device (for example a file-backed capture device, or a pre-roll
    ///     device replaying its buffered audio) can emit blocks before <c>Start()</c> returns, and
    ///     a finite source's bounded channel would otherwise block its reader with nothing
    ///     draining it.
    ///     </para>
    /// </remarks>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SessionStateChangedEventArgs startingArgs;
        Task deviceStartTask;

        lock (_syncRoot)
        {
            if (_state != RecognitionSessionState.Created)
            {
                throw new InvalidOperationException(
                    "Cannot start recognition: this session has already been started, stopped, or disposed. " +
                    "Sessions are single-use; create a new session via ISpeechRecognizerEngine.CreateSessionAsync to run again.");
            }

            startingArgs = TransitionTo(RecognitionSessionState.Starting);

            // A live microphone must never stall its audio callback, and must never lose audio
            // either, so it queues into an unbounded channel whose growth OnFrameCaptured bounds
            // with a reported safety cap. A finite file is delivered faster than real time on its
            // own reader thread, so it queues into a small bounded channel that applies
            // backpressure to that reader instead.
            var frames = _isFiniteSource
                ? Channel.CreateBounded<float[]>(
                    new BoundedChannelOptions(FiniteSourcePendingFrameCapacity)
                    {
                        FullMode = BoundedChannelFullMode.Wait,
                        SingleReader = true,
                        SingleWriter = false
                    })
                : Channel.CreateUnbounded<float[]>(
                    new UnboundedChannelOptions
                    {
                        SingleReader = true,
                        SingleWriter = false
                    });
            _pendingFrames = frames;

            // Start the pump thread first so the channel is already being drained the instant
            // the device starts emitting blocks (see the remarks above).
            _pumpCts = new CancellationTokenSource();
            _pumpTask = _worker.RunAsync(PumpLoop, _pumpCts.Token, out var pumpCompletion);
            _pumpRawCompletion = pumpCompletion;

            Interlocked.Exchange(ref _acceptingFrames, 1);
            _device.FrameCaptured += OnFrameCaptured;

            // Run the native, potentially slow device.Start() call through the same dedicated
            // worker the pump loop uses, rather than inline on this caller's thread (see this
            // method's remarks). Given this call's own cancellationToken so a canceled start is
            // genuinely abandoned (not silently ignored) rather than always running to completion
            // regardless of the caller's request. The raw completion is published to
            // _deviceStartRawCompletion before the lock is released so RunTeardownAsync can still
            // await it before ever stopping the device.
            deviceStartTask = _worker.RunAsync(_ => _device.Start(), cancellationToken, out var deviceStartRawCompletion);
            _deviceStartRawCompletion = deviceStartRawCompletion;
        }

        // Raised only after _syncRoot has been released: invoking a host's
        // StateChanged handler while still holding this session's state lock risks deadlock if
        // that handler calls back into this session (for example StopAsync/DisposeAsync) and then
        // synchronously blocks on the result, since any continuation that needs this same lock
        // could never run while this thread holds it.
        RaiseStateChanged(startingArgs);

        SessionStateChangedEventArgs? finalArgs;
        SpeechRecognizerUnavailableException? startFailure = null;
        try
        {
            await deviceStartTask.ConfigureAwait(false);

            lock (_syncRoot)
            {
                // Only advance from Starting: a concurrent StopAsync/DisposeAsync/FaultSession may
                // already have moved this session on (to Stopping/Stopped, or a differently-caused
                // Faulted) while the device was still starting, in which case that outcome stands.
                finalArgs = _state == RecognitionSessionState.Starting
                    ? TransitionTo(RecognitionSessionState.Running)
                    : null;
            }
        }
        catch (Exception ex)
        {
            lock (_syncRoot)
            {
                if (_state == RecognitionSessionState.Starting)
                {
                    // Roll back the subscription and let the already-running pump drain out and
                    // exit on its own (it reacts only to the channel completing, never to
                    // _pumpCts - see PumpLoop's remarks) so this session leaks neither the
                    // subscription nor the dedicated pump thread even if nobody ever calls
                    // StopAsync/DisposeAsync on this now-Faulted session. Resetting the backend
                    // and stopping the device are still deferred to that eventual teardown call,
                    // exactly as for any other fault, since releasing the engine's lease still
                    // requires one.
                    _device.FrameCaptured -= OnFrameCaptured;
                    Interlocked.Exchange(ref _acceptingFrames, 0);
                    _pendingFrames.Writer.TryComplete();
                    finalArgs = TransitionTo(RecognitionSessionState.Faulted);
                }
                else
                {
                    // A concurrent teardown already moved this session on while the device was
                    // still starting; it already unsubscribed/completed the channel itself.
                    finalArgs = null;
                }
            }

            if (finalArgs is not null)
            {
                _diagnostics.Report(
                    SpeechDiagnosticLevel.Error,
                    DiagnosticsCategory,
                    $"Failed to start recognition because the capture device could not start: {ex.Message}");
                _resultBuffer.Fault(ex);
                startFailure = new SpeechRecognizerUnavailableException(
                    "Cannot start recognition: the capture device failed to start.",
                    ex);
            }
        }

        if (finalArgs is not null)
        {
            RaiseStateChanged(finalArgs);
        }

        if (startFailure is not null)
        {
            throw startFailure;
        }

        _diagnostics.Report(SpeechDiagnosticLevel.Info, DiagnosticsCategory, "Started streaming recognition.");
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Idempotent and safe to call concurrently, including while this session is
    ///     <see cref="RecognitionSessionState.Faulted"/>: every caller (and <see cref="FaultSession"/>
    ///     and <see cref="DisposeAsync"/>) shares the single teardown operation started by
    ///     whichever of them gets there first, so the destructive steps - draining the pump,
    ///     resetting the shared backend, stopping the device - run exactly once no matter how many
    ///     callers are racing. A faulted session still runs this same teardown so the capture
    ///     device and shared backend are genuinely released, but its reported <see cref="State"/>
    ///     stays <see cref="RecognitionSessionState.Faulted"/> rather than advancing to
    ///     <see cref="RecognitionSessionState.Stopped"/>, preserving the fault for
    ///     <see cref="GetResultsAsync"/> consumers.
    ///     <para>
    ///     Without cancellation this is a graceful stop: the returned task completes only after
    ///     every queued block has been decoded, the backend has been flushed, and every final
    ///     result has been buffered. Canceling <paramref name="cancellationToken"/> - before or
    ///     during the call - requests a discarding stop instead (see the type remarks): queued
    ///     audio is dropped, no flush is performed, and this caller's wait ends with
    ///     <see cref="OperationCanceledException"/>, while the shared teardown still converges
    ///     for every other caller.
    ///     </para>
    /// </remarks>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task teardownTask;
        SessionStateChangedEventArgs? transition;
        TeardownStart? start;
        lock (_syncRoot)
        {
            // An already-canceled token must take effect before the pump can observe the
            // completed channel, so not a single queued block is decoded or flushed afterwards.
            if (cancellationToken.IsCancellationRequested)
            {
                _discardRequested = true;
            }

            if (_state is RecognitionSessionState.Disposing or RecognitionSessionState.Disposed)
            {
                teardownTask = Task.CompletedTask;
                transition = null;
                start = null;
            }
            else
            {
                (teardownTask, transition, start) = EnsureTeardownStartedLocked(preserveFault: _state == RecognitionSessionState.Faulted);
            }
        }

        // Raised only after _syncRoot has been released : see StartAsync's remarks.
        if (transition is not null)
        {
            RaiseStateChanged(transition);
        }

        // Started only after the transition above has been raised, and only on this thread's own
        // sequential program order: RunTeardownAsync must never begin running -
        // not even the portion of it that could complete synchronously given fast/trivial
        // dependencies - until this caller is done raising its own transition, or a concurrent
        // StateChanged subscriber could observe the eventual Stopped/Faulted-preserved transition
        // before the Stopping transition that must logically precede it.
        if (start is { } pending)
        {
            _ = RunTeardownAsync(pending);
        }

        // A canceled caller never waits: report the cancellation deterministically, even if the
        // (discarding) teardown happens to have finished already.
        if (cancellationToken.IsCancellationRequested)
        {
            RequestDiscard();
            return Task.FromCanceled(cancellationToken);
        }

        // This is the one, unconditional return statement for a caller that has not (yet)
        // canceled, so it is always reached regardless of which branch above produced
        // teardownTask.
        return cancellationToken.CanBeCanceled
            ? WaitForTeardownAsync(teardownTask, cancellationToken)
            : teardownTask;
    }

    /// <summary>
    ///     Waits for the shared teardown on behalf of one <see cref="StopAsync"/> caller whose
    ///     token may still be canceled, turning a cancellation into a discarding stop.
    /// </summary>
    /// <param name="teardownTask">The shared teardown task every stop/dispose caller observes.</param>
    /// <param name="cancellationToken">This caller's token; canceling it requests a discard.</param>
    /// <returns>A task that completes with the teardown, or as canceled once the token is canceled.</returns>
    private async Task WaitForTeardownAsync(Task teardownTask, CancellationToken cancellationToken)
    {
        try
        {
            await teardownTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Requested before this caller observes its cancellation, so by the time the caller
            // sees OperationCanceledException the pump is already discarding. (A token canceled
            // after the teardown already finished never reaches here: there is nothing left to
            // discard.)
            RequestDiscard();
            throw;
        }
    }

    /// <summary>
    ///     Switches the in-flight teardown to a discarding stop: the pump drops every block still
    ///     queued, skips the final flush, and the abandon deadline for a stuck backend call starts
    ///     immediately rather than waiting for the pump to stall.
    /// </summary>
    /// <remarks>
    ///     Safe to call from any thread, any number of times, including after teardown has
    ///     disposed the pump's cancellation source (the late cancel is then a no-op).
    /// </remarks>
    private void RequestDiscard()
    {
        CancellationTokenSource? pumpCts;
        lock (_syncRoot)
        {
            _discardRequested = true;
            pumpCts = _pumpCts;
        }

        CancelQuietly(pumpCts);
    }

    /// <summary>
    ///     Cancels <paramref name="cancellationSource"/>, tolerating a source that teardown has
    ///     already disposed concurrently.
    /// </summary>
    /// <param name="cancellationSource">The source to cancel, or <see langword="null"/> for none.</param>
    private static void CancelQuietly(CancellationTokenSource? cancellationSource)
    {
        try
        {
            cancellationSource?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Teardown already finished with this source; there is nothing left to cancel.
        }
    }

    /// <summary>
    ///     The captured inputs <see cref="RunTeardownAsync"/> needs to actually run the teardown
    ///     sequence, plus the <see cref="TaskCompletionSource"/> that backs the shared,
    ///     already-published <see cref="_teardownTask"/> - deliberately separated from starting
    ///     <see cref="RunTeardownAsync"/> itself: the caller that receives this
    ///     must invoke <see cref="RunTeardownAsync"/> only after it has released
    ///     <see cref="_syncRoot"/> and raised its own state-transition event, never while still
    ///     holding the lock, so the eventual Stopped/Faulted-preserved transition that
    ///     <see cref="RunTeardownAsync"/> raises can never be observed by a subscriber before the
    ///     Stopping transition that must logically precede it.
    /// </summary>
    private readonly record struct TeardownStart(
        Task? PumpTask,
        Task? PumpRawCompletion,
        CancellationTokenSource? PumpCts,
        Task? DeviceStartRawCompletion,
        bool PreserveFault,
        TaskCompletionSource CompletionSource);

    /// <summary>
    ///     Returns the single, lazily-created teardown task shared by <see cref="StopAsync"/>,
    ///     <see cref="FaultSession"/>, and <see cref="DisposeAsync"/>, together with the
    ///     state-transition event (if any) that the caller must raise via
    ///     <see cref="RaiseStateChanged"/>, and - only for whichever caller actually wins the
    ///     race to create it - the <see cref="TeardownStart"/> that caller alone must then pass to
    ///     <see cref="RunTeardownAsync"/> once it has released <see cref="_syncRoot"/> and raised
    ///     its transition. Must be called with <see cref="_syncRoot"/> held, so
    ///     creating it (including the synchronous unsubscribe/channel-completion below) is atomic
    ///     with every state check a concurrent caller might make - but, deliberately, this method
    ///     never itself invokes <see cref="RunTeardownAsync"/>: doing so here, while
    ///     <see cref="_syncRoot"/> may be held reentrantly by an outer caller (<see cref="StopAsync"/>,
    ///     <see cref="DisposeCoreAsync"/>, <see cref="FaultSession"/>), risks that method - an
    ///     ordinary <c>async Task</c> method - running synchronously to completion (because every
    ///     awaited dependency happens to already be complete) before this call returns, which would
    ///     let it raise the Stopped/Faulted-preserved transition before the Stopping transition
    ///     this method itself just produced has had a chance to be raised by the outer caller.
    /// </summary>
    /// <param name="preserveFault">
    ///     Whether the session was already <see cref="RecognitionSessionState.Faulted"/> when
    ///     teardown was first requested, so the final state transition below preserves it instead
    ///     of advancing to <see cref="RecognitionSessionState.Stopped"/>.
    /// </param>
    private (Task Teardown, SessionStateChangedEventArgs? Transition, TeardownStart? Start) EnsureTeardownStartedLocked(bool preserveFault)
    {
        if (_teardownTask is not null)
        {
            return (_teardownTask, null, null);
        }

        if (_state == RecognitionSessionState.Created)
        {
            // Never started: there is nothing to drain, reset, or stop, but single-use
            // bookkeeping still requires converging on Stopped - and GetResultsAsync still must
            // not hang forever waiting for a result buffer nobody ever completes.
            var createdArgs = TransitionTo(RecognitionSessionState.Stopped);
            _resultBuffer.Complete();
            _teardownTask = Task.CompletedTask;
            return (_teardownTask, createdArgs, null);
        }

        SessionStateChangedEventArgs? stoppingArgs = null;
        if (_state is RecognitionSessionState.Starting or RecognitionSessionState.Running or RecognitionSessionState.Stopping)
        {
            stoppingArgs = TransitionTo(RecognitionSessionState.Stopping);
        }

        // Unsubscribing and closing the acceptance gate synchronously, while still holding the
        // lock, is the point at which this stop is accepted: no block whose callback starts after
        // this is queued. A live source's queue is completed later by RunTeardownAsync, only once
        // every callback that was already running has queued its block (see OnFrameCaptured), so
        // that block is not lost; this never blocks here, because a live callback never waits.
        // A finite file source's queue is completed right away instead: its reader thread may be
        // blocked on backpressure, and completing the queue is what releases it - the rest of a
        // stopped file is not part of the session.
        _device.FrameCaptured -= OnFrameCaptured;
        Interlocked.Exchange(ref _acceptingFrames, 0);
        if (_isFiniteSource)
        {
            _pendingFrames?.Writer.TryComplete();
        }

        // Publish a not-yet-running placeholder task immediately so every concurrent caller
        // still observes exactly one shared teardown operation; only the winning caller actually
        // starts running it, and only after leaving this lock (see this method's remarks).
        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _teardownTask = completionSource.Task;
        var start = new TeardownStart(_pumpTask, _pumpRawCompletion, _pumpCts, _deviceStartRawCompletion, preserveFault, completionSource);
        return (_teardownTask, stoppingArgs, start);
    }

    /// <summary>
    ///     Drains the pump loop, then finalizes this session's state - and, unless the pump worker
    ///     was abandoned, resets the shared recognition backend and stops the capture device
    ///     immediately - the one destructive teardown sequence shared by every caller through
    ///     <see cref="EnsureTeardownStartedLocked"/>.
    /// </summary>
    /// <param name="start">
    ///     The teardown inputs captured under <see cref="_syncRoot"/> by
    ///     <see cref="EnsureTeardownStartedLocked"/>, including the <see cref="TaskCompletionSource"/>
    ///     backing the already-published, shared teardown task.
    /// </param>
    /// <remarks>
    ///     This session's own state still converges (to <see cref="RecognitionSessionState.Stopped"/>
    ///     or the preserved fault) and the result buffer still completes promptly here even when
    ///     the pump worker was abandoned, matching <see cref="StopAsync"/>'s documented contract
    ///     that its returned task completing means this session itself has converged. Only
    ///     resetting the shared backend and stopping the capture device - which must never run
    ///     concurrently with the pump thread still being inside a blocking backend call - is deferred to <see cref="_deferredBackendTeardown"/>, a continuation of
    ///     <paramref name="start"/>'s pump raw completion that <see cref="DisposeCoreAsync"/>
    ///     awaits before releasing the engine's lease, which is what actually closes the reuse
    ///     race the abandon guarantee must handle: a new session (or engine disposal) touching the shared
    ///     backend concurrently with an abandoned pump thread that has not yet genuinely exited.
    ///     <para>
    ///     Must only ever be invoked after the caller that obtained <paramref name="start"/> from
    ///     <see cref="EnsureTeardownStartedLocked"/> has released <see cref="_syncRoot"/> and
    ///     raised its own Stopping/Faulted transition: see
    ///     <see cref="TeardownStart"/>'s and <see cref="EnsureTeardownStartedLocked"/>'s remarks
    ///     for why. <paramref name="start"/>'s <see cref="TaskCompletionSource"/> is always
    ///     completed, even if an earlier step reports a failure via diagnostics, since every step
    ///     below already converts its own failures to diagnostics rather than throwing.
    ///     </para>
    /// </remarks>
    private async Task RunTeardownAsync(TeardownStart start)
    {
        var (pumpTask, pumpRawCompletion, pumpCts, deviceStartRawCompletion, preserveFault, completionSource) = start;
        try
        {
            // Every caller invokes this fire-and-forget (`_ = RunTeardownAsync(...)`), relying on
            // control returning to it immediately so it never blocks on this method's own
            // potentially-slow/native work (backend reset, device stop). Awaiting an
            // already-completed task does not yield - it continues synchronously on the calling
            // thread - so without this unconditional yield, a null/already-canceled pumpCts or an
            // already-completed pumpTask would let the whole method, including the blocking
            // device.Stop() call below, run inline on whichever thread happened to call
            // StopAsync/DisposeAsync/FaultSession, defeating their documented
            // teardown-runs-in-the-background contract.
            await Task.Yield();

            // Awaited before anything below ever touches the device: StartAsync may still be
            // mid-flight on its own native IAudioCaptureDevice.Start() call (run through _worker,
            // never under _syncRoot - see its remarks), and device.Stop() below must never run
            // concurrently with that still-in-progress device.Start() call. Awaiting the raw
            // completion here (not StartAsync's own abandon-aware task) is deliberate: if a
            // caller canceled StartAsync while the device was still starting and the abandon
            // timeout has already elapsed, StartAsync's own task completes early as canceled/
            // faulted while the device-start worker thread may still genuinely be inside
            // IAudioCaptureDevice.Start() - this raw completion only resolves once that thread has
            // truly exited, exactly mirroring _pumpRawCompletion's role for the pump thread. This
            // is also what keeps this session's documented invariant intact now that StartAsync no
            // longer blocks its own caller for the device call's duration: this shared teardown
            // task - and therefore any concurrent StopAsync/DisposeAsync awaiting it - cannot
            // complete until the device has genuinely finished starting. Any exception from a
            // failed/abandoned start is already reported and surfaced by StartAsync's own
            // continuation; nothing further to do with it here.
            if (deviceStartRawCompletion is not null)
            {
                try
                {
                    await deviceStartRawCompletion.ConfigureAwait(false);
                }
                catch
                {
                    // Already handled/reported by StartAsync's own continuation.
                }
            }

            // A live source's queue is completed only now, once every capture callback that was
            // already running when the stop was accepted has queued its block (see
            // OnFrameCaptured and EnsureTeardownStartedLocked). Live callbacks never block, so this
            // wait is normally a few microseconds; it is bounded by the abandon interval anyway so
            // a pathological host handler can never hang teardown.
            if (!_isFiniteSource)
            {
                SpinWait.SpinUntil(() => Volatile.Read(ref _callbacksInFlight) == 0, _worker.AbandonTimeout);
                _pendingFrames?.Writer.TryComplete();
            }

            // The pump thread itself never observes pumpCts while draining (see PumpLoop's
            // remarks); canceling it only starts DedicatedWorker's abandon deadline for a pump
            // thread genuinely stuck inside a blocking backend call. A graceful stop therefore
            // waits for as long as the pump keeps making progress - a long backlog or a slow
            // flush is drained in full - and requests cancellation only once the pump has made
            // no progress for a whole abandon interval. A graceful stop of a finite file source is
            // never canceled here at all: the abandon deadline would cut off a slow backend (for
            // example a large CPU model) still decoding the file's queued audio, losing the tail
            // of a transcript the user expects in totality. A discarding stop (a canceled
            // StopAsync) of any source, file or live, has already canceled pumpCts itself through
            // RequestDiscard, which starts the abandon deadline immediately, so even a stuck
            // backend cannot hang a canceled stop.
            if (pumpTask is not null && pumpCts is not null && !_isFiniteSource)
            {
                await CancelPumpWhenStalledAsync(pumpTask, pumpCts).ConfigureAwait(false);
            }

            var abandoned = false;
            if (pumpTask is not null)
            {
                try
                {
                    await pumpTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // The pump thread may still be inside a blocking backend call
                    // (AcceptSamples/TryDecode/TryFlush) even though the abandon-aware wrapper above
                    // has given up waiting for it: backend.Reset()/device.Stop() below
                    // must not run yet, or they would race that still-running call.
                    abandoned = true;
                }
                catch (Exception ex)
                {
                    _diagnostics.Report(
                        SpeechDiagnosticLevel.Error,
                        DiagnosticsCategory,
                        $"The recognition pump loop ended with a fault: {ex.Message}");
                }
            }

            lock (_syncRoot)
            {
                // RequestDiscard reads _pumpCts under this lock; clearing it first means a late
                // cancellation never targets the source disposed below.
                _pumpCts = null;
            }

            pumpCts?.Dispose();
            _resultBuffer.Complete();
            ReportDroppedLiveAudio();

            if (abandoned)
            {
                // Defer only the backend reset/device stop until the pump thread's own raw
                // completion genuinely happens; DisposeCoreAsync awaits this before
                // releasing the engine's lease. This call itself still completes promptly and
                // best-effort, matching the existing guarantee that an abandoned
                // stop/dispose never hangs its caller.
                lock (_syncRoot)
                {
                    _deferredBackendTeardown = ResetBackendAndStopDeviceAsync(pumpRawCompletion);
                }
            }
            else
            {
                // The pump thread already genuinely exited (it was not abandoned above, or there was
                // no pump to begin with), so it is safe to reset the backend and stop the device now.
                ResetBackendAndStopDeviceCore();
            }

            SessionStateChangedEventArgs? stoppedArgs = null;
            lock (_syncRoot)
            {
                if (!preserveFault && _state == RecognitionSessionState.Stopping)
                {
                    stoppedArgs = TransitionTo(RecognitionSessionState.Stopped);
                }
            }

            // Raised only after _syncRoot has been released: see StartAsync's remarks.
            if (stoppedArgs is not null)
            {
                RaiseStateChanged(stoppedArgs);
            }

            _diagnostics.Report(SpeechDiagnosticLevel.Info, DiagnosticsCategory, "Stopped streaming recognition.");
        }
        finally
        {
            // Always completes the shared, previously-published teardown task,
            // regardless of which branch above ran or whether an unexpected exception escaped one
            // of them, so no concurrent StopAsync/DisposeAsync/FaultSession caller can ever hang
            // waiting on a teardown that silently stopped making progress.
            completionSource.TrySetResult();
        }
    }

    /// <summary>
    ///     Waits for <paramref name="pumpTask"/> for as long as the pump thread keeps making
    ///     progress, and cancels <paramref name="pumpCts"/> - starting the dedicated worker's
    ///     abandon deadline - only once a whole abandon interval passes with no progress at all.
    /// </summary>
    /// <param name="pumpTask">The abandon-aware pump task being drained.</param>
    /// <param name="pumpCts">The pump's cancellation source, canceled only when the pump stalls.</param>
    /// <remarks>
    ///     A fixed deadline from the moment of stopping would cut off a legitimate drain - a
    ///     backlog of queued live audio, or a flush that decodes padding - and silently lose the
    ///     final result the caller is waiting for. Measuring progress instead keeps the abandon
    ///     guarantee for a genuinely stuck native call (no progress at all) while never
    ///     discarding audio a working recognizer is still decoding.
    /// </remarks>
    private async Task CancelPumpWhenStalledAsync(Task pumpTask, CancellationTokenSource pumpCts)
    {
        var lastProgress = Interlocked.Read(ref _pumpProgress);
        while (!pumpTask.IsCompleted)
        {
            // The delay is cancelable so its timer is released as soon as the pump finishes.
            using var intervalCts = new CancellationTokenSource();
            var interval = Task.Delay(_worker.AbandonTimeout, intervalCts.Token);
            var finished = await Task.WhenAny(pumpTask, interval).ConfigureAwait(false);
            if (finished == pumpTask)
            {
                await intervalCts.CancelAsync().ConfigureAwait(false);
                return;
            }

            var progress = Interlocked.Read(ref _pumpProgress);
            if (progress == lastProgress)
            {
                // No backend step completed for a whole interval: treat the pump as stuck.
                CancelQuietly(pumpCts);
                return;
            }

            lastProgress = progress;
        }
    }

    /// <summary>
    ///     Reports, once at teardown, how much live audio the safety cap discarded during this
    ///     session, so a host investigating missing words can see the total rather than only the
    ///     first overflow warning.
    /// </summary>
    private void ReportDroppedLiveAudio()
    {
        var dropped = Interlocked.Read(ref _droppedLiveSamples);
        if (dropped > 0)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                $"Recognition fell more than {DescribeCap()} behind live capture; {dropped} captured samples were discarded during this session.");
        }
    }

    /// <summary>Formats the live safety cap for diagnostics.</summary>
    /// <returns>The cap in seconds of device audio, for example <c>60 s</c>.</returns>
    private string DescribeCap()
    {
        var samplesPerSecond = Math.Max(1L, (long)_resampler.SourceSampleRate * _resampler.SourceChannelCount);
        return $"{_maxPendingLiveSamples / (double)samplesPerSecond:0.###} s";
    }

    /// <summary>
    ///     Awaits <paramref name="pumpRawCompletion"/> (the pump thread's own raw completion),
    ///     then resets the shared recognition backend and stops the capture device - used only
    ///     when the pump worker was abandoned, so the shared backend is never
    ///     touched while that thread may still be inside it.
    /// </summary>
    private async Task ResetBackendAndStopDeviceAsync(Task? pumpRawCompletion)
    {
        if (pumpRawCompletion is not null)
        {
            try
            {
                await pumpRawCompletion.ConfigureAwait(false);
            }
            catch
            {
                // Already reported by RunTeardownAsync if the pump task converged or faulted
                // normally; if it was instead abandoned, this only proves the thread has
                // genuinely exited - nothing further to report here beyond that guarantee.
            }
        }

        ResetBackendAndStopDeviceCore();
    }

    /// <summary>
    ///     Resets the shared recognition backend and stops the capture device, reporting (rather
    ///     than throwing) any failure from either step.
    /// </summary>
    private void ResetBackendAndStopDeviceCore()
    {
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
    /// <remarks>
    ///     Every concurrent caller shares the same single-flight disposal task rather than a second call returning the instant the first
    ///     merely begins: both wait for the exact same real teardown, state transitions, and
    ///     engine-lease release to complete.
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        lock (_syncRoot)
        {
            _disposeTask ??= DisposeCoreAsync();
            return new ValueTask(_disposeTask);
        }
    }

    /// <summary>
    ///     Runs the full Stopping -&gt; Stopped (or Faulted-preserving) teardown, then additionally
    ///     awaits the deferred backend reset/device stop continuation set when the pump worker was
    ///     abandoned - which itself awaits the pump thread's own raw completion, not
    ///     merely the abandon-aware task the teardown above already waited for - before
    ///     transitioning through Disposing to Disposed and releasing the engine's exclusivity
    ///     lease. This closes the reuse race: even though <see cref="StopAsync"/> deliberately
    ///     does not wait out a non-cooperative, abandoned native call, disposal must, because
    ///     releasing the lease is what permits a new session (or engine disposal) to touch or
    ///     dispose the shared backend - and that must never overlap an abandoned pump thread that
    ///     has not genuinely exited yet. Started at most once; see <see cref="_disposeTask"/>.
    /// </summary>
    private async Task DisposeCoreAsync()
    {
        Task teardown;
        SessionStateChangedEventArgs? transition;
        TeardownStart? start;
        lock (_syncRoot)
        {
            (teardown, transition, start) = EnsureTeardownStartedLocked(preserveFault: _state == RecognitionSessionState.Faulted);
        }

        // Raised only after _syncRoot has been released: see StartAsync's remarks.
        if (transition is not null)
        {
            RaiseStateChanged(transition);
        }

        // Started only after the transition above has been raised: see
        // StopAsync's matching comment and TeardownStart's remarks for why.
        if (start is { } pending)
        {
            _ = RunTeardownAsync(pending);
        }

        await teardown.ConfigureAwait(false);

        Task? deferredBackendTeardown;
        lock (_syncRoot)
        {
            deferredBackendTeardown = _deferredBackendTeardown;
        }

        if (deferredBackendTeardown is not null)
        {
            try
            {
                await deferredBackendTeardown.ConfigureAwait(false);
            }
            catch
            {
                // FinishBackendTeardownAsync already reports every failure through diagnostics
                // itself; this only confirms the abandoned pump thread - and the backend
                // reset/device stop that had to wait for it - has genuinely finished
                // before the lease below is released.
            }
        }

        SessionStateChangedEventArgs disposingArgs;
        SessionStateChangedEventArgs disposedArgs;
        lock (_syncRoot)
        {
            disposingArgs = TransitionTo(RecognitionSessionState.Disposing);
            disposedArgs = TransitionTo(RecognitionSessionState.Disposed);
        }

        RaiseStateChanged(disposingArgs);
        RaiseStateChanged(disposedArgs);

        _releaseLease();
    }

    /// <summary>
    ///     Moves this session to <paramref name="newState"/> and returns the event args the caller
    ///     must raise via <see cref="RaiseStateChanged"/> once <see cref="_syncRoot"/> has been
    ///     released. Must be called under <see cref="_syncRoot"/>, but deliberately
    ///     does not invoke <see cref="StateChanged"/> itself: a host's handler calling back into
    ///     this session (for example <see cref="StopAsync"/>/<see cref="DisposeAsync"/>) and then
    ///     synchronously blocking on the result would otherwise risk deadlocking against this same
    ///     lock, since the resulting task's continuation could never run while this thread holds it.
    /// </summary>
    private SessionStateChangedEventArgs TransitionTo(RecognitionSessionState newState)
    {
        var previous = _state;
        _state = newState;
        return new SessionStateChangedEventArgs(previous, newState);
    }

    /// <summary>
    ///     Invokes <see cref="StateChanged"/> for <paramref name="args"/>. Must be called only
    ///     after releasing <see cref="_syncRoot"/>; never called while holding it.
    /// </summary>
    private void RaiseStateChanged(SessionStateChangedEventArgs args)
    {
        try
        {
            StateChanged?.Invoke(this, args);
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
    /// <remarks>
    ///     Unsubscribing does not wait for a callback that is already running, so this method and
    ///     teardown share a barrier: the callback first counts itself in
    ///     <see cref="_callbacksInFlight"/> and then checks <see cref="_acceptingFrames"/>, while
    ///     teardown first clears <see cref="_acceptingFrames"/> (the moment the stop is accepted)
    ///     and then, for a live source, waits for the in-flight count to reach zero before it
    ///     completes the queue. Both steps use full fences, so either the callback sees the stop
    ///     and drops a block that arrived after it, or teardown sees the callback and waits for its
    ///     block to be queued - a block captured before the stop is never lost to the race.
    /// </remarks>
    private void OnFrameCaptured(object? sender, AudioCaptureFrameEventArgs e)
    {
        Interlocked.Increment(ref _callbacksInFlight);
        try
        {
            if (Volatile.Read(ref _acceptingFrames) == 0)
            {
                // The stop was accepted before this block arrived; it is not part of the session.
                return;
            }

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

            if (_isFiniteSource)
            {
                // Runs on the file device's own Start() thread, never a live audio callback, so
                // blocking here until the pump catches up is safe and loses no audio. The channel
                // is completed on teardown or a pump fault, which releases a blocked write.
                try
                {
                    frames.Writer.WriteAsync([.. e.Samples], CancellationToken.None).AsTask().GetAwaiter().GetResult();
                }
                catch (ChannelClosedException)
                {
                    // The session is stopping or faulted; the remaining file audio is moot.
                }

                return;
            }

            EnqueueLiveBlock(frames.Writer, e.Samples);
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
        finally
        {
            Interlocked.Decrement(ref _callbacksInFlight);
        }
    }

    /// <summary>
    ///     Queues one live block without ever blocking the audio callback, enforcing the reported
    ///     safety cap on the queued backlog.
    /// </summary>
    /// <param name="writer">The pending-frame channel's writer (unbounded for a live source).</param>
    /// <param name="samples">The captured interleaved samples to copy and queue.</param>
    /// <remarks>
    ///     A block is always accepted when nothing else is queued, so even a single block larger
    ///     than the cap is still recognized; otherwise a block that would grow the backlog beyond
    ///     the cap is discarded, counted, and - for the first such block of the session -
    ///     reported as a warning diagnostic. Nothing is ever dropped silently.
    /// </remarks>
    private void EnqueueLiveBlock(ChannelWriter<float[]> writer, IReadOnlyList<float> samples)
    {
        var count = samples.Count;
        var pending = Interlocked.Add(ref _pendingLiveSamples, count);
        if (pending > _maxPendingLiveSamples && pending != count)
        {
            Interlocked.Add(ref _pendingLiveSamples, -count);
            Interlocked.Add(ref _droppedLiveSamples, count);
            if (Interlocked.Exchange(ref _overflowReported, 1) == 0)
            {
                _diagnostics.Report(
                    SpeechDiagnosticLevel.Warning,
                    DiagnosticsCategory,
                    $"Recognition has fallen more than {DescribeCap()} behind live capture; newly captured audio is being discarded until it catches up.");
            }

            return;
        }

        if (!writer.TryWrite([.. samples]))
        {
            // Teardown completes a live queue only after every in-flight callback has finished
            // (see OnFrameCaptured), so a failed write here means the pump faulted and closed
            // the queue itself: the session is already faulted and the block cannot be used.
            Interlocked.Add(ref _pendingLiveSamples, -count);
        }
    }

    /// <summary>
    ///     Transitions this session to <see cref="RecognitionSessionState.Faulted"/>, surfaces
    ///     <paramref name="cause"/> through any active <see cref="GetResultsAsync"/> enumeration,
    ///     and starts the same teardown <see cref="StopAsync"/> uses - preserving the fault - so
    ///     the capture device and shared backend are genuinely released even though this method
    ///     itself runs synchronously on the capture callback thread and cannot await the result.
    /// </summary>
    private void FaultSession(Exception cause)
    {
        SessionStateChangedEventArgs? faultedArgs = null;
        SessionStateChangedEventArgs? teardownArgs = null;
        TeardownStart? start = null;
        lock (_syncRoot)
        {
            if (_state is RecognitionSessionState.Starting or RecognitionSessionState.Running or RecognitionSessionState.Stopping)
            {
                faultedArgs = TransitionTo(RecognitionSessionState.Faulted);
                (_, teardownArgs, start) = EnsureTeardownStartedLocked(preserveFault: true);
            }
        }

        // Raised only after _syncRoot has been released: see StartAsync's remarks.
        if (faultedArgs is not null)
        {
            RaiseStateChanged(faultedArgs);
        }

        if (teardownArgs is not null)
        {
            RaiseStateChanged(teardownArgs);
        }

        // Started only after both transitions above have been raised: see
        // StopAsync's matching comment and TeardownStart's remarks for why. Fire-and-forget is
        // safe here: RunTeardownAsync reports every failure through diagnostics itself rather
        // than letting any step throw, so there is nothing this caller needs to observe beyond
        // having started it.
        if (start is { } pending)
        {
            _ = RunTeardownAsync(pending);
        }

        _resultBuffer.Fault(cause);
    }

    /// <summary>
    ///     Drains queued capture blocks through the recognition pipeline until the queue is
    ///     completed, then flushes any trailing audio. Runs synchronously on the dedicated pump
    ///     thread (see <see cref="DedicatedWorker"/>).
    /// </summary>
    /// <remarks>
    ///     Deliberately does not observe the <see cref="DedicatedWorker"/> cancellation token
    ///     passed to <see cref="DedicatedWorker.RunAsync(Action{CancellationToken}, CancellationToken)"/>: that token is canceled by
    ///     <see cref="StopAsync"/> purely as the outer abandon-timeout deadline, and
    ///     reading it here as well would risk silently dropping buffered-but-undecoded frames in
    ///     a race with that cancellation, breaking the guarantee that every block accepted before
    ///     <see cref="StopAsync"/> is still decoded. Completion of <see cref="_pendingFrames"/>'s
    ///     writer is the only signal this loop reacts to. A discarding stop (see
    ///     <see cref="RequestDiscard"/>) still reads the channel to its end, so a finite source's
    ///     blocked reader is released, but drops each block instead of decoding it and skips the
    ///     final flush.
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

            if (!_isFiniteSource)
            {
                Interlocked.Add(ref _pendingLiveSamples, -frame.Length);
            }

            if (_discardRequested)
            {
                continue;
            }

            if (!ProcessFrame(frame))
            {
                // The backend itself failed: continuing to pump further frames
                // through a backend that just threw would only repeat the same failure for every
                // subsequent block, while leaving this session Running and its result buffer open
                // forever for a caller still awaiting GetResultsAsync. Stop draining; the session
                // has already been faulted (result buffer included) from the pump thread itself.
                break;
            }

            Interlocked.Increment(ref _pumpProgress);
        }

        if (!_discardRequested)
        {
            FlushFinal();
        }
    }

    /// <summary>
    ///     Finalizes and buffers any trailing audio the backend has accepted but not yet decoded,
    ///     as the very last action of the pump loop.
    /// </summary>
    private void FlushFinal()
    {
        try
        {
            // Results beyond MaxResultsPerFrame for the last block(s) are still queued in the
            // backend; drain them (bounded) before finalizing so none is lost at stop.
            for (var i = 0; i < MaxResultsAtFinalDrain; i++)
            {
                if (!_backend.TryDecode(out var pending) || pending is null)
                {
                    break;
                }

                BufferResult(pending);
                Interlocked.Increment(ref _pumpProgress);
            }

            var flushed = _backend.TryFlush(out var result);
            Interlocked.Increment(ref _pumpProgress);
            if (!flushed || result is null)
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
    /// <returns>
    ///     <see langword="false"/> if the backend itself threw, meaning this
    ///     session has already been faulted and the pump loop must not call this again;
    ///     otherwise <see langword="true"/>.
    /// </returns>
    private bool ProcessFrame(float[] interleavedSamples)
    {
        try
        {
            var monoSamples = _resampler.Convert(interleavedSamples);
            _backend.AcceptSamples(monoSamples);

            for (var i = 0; i < MaxResultsPerFrame; i++)
            {
                if (!_backend.TryDecode(out var result) || result is null)
                {
                    return true;
                }

                BufferResult(result);
            }

            return true;
        }
        catch (Exception ex)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"A captured audio block could not be recognized: {ex.Message}");

            // Unlike the device-unavailable path (FaultSession), this runs ON the pump thread
            // itself: FaultSession's teardown would await _pumpTask/_pumpRawCompletion, which
            // represent this very thread, so starting teardown here would self-deadlock. Faulting
            // the session's state and result buffer is safe to do directly; the pump thread is
            // about to exit on its own (PumpLoop breaks right after this returns false), so the
            // backend is not touched further, and whichever of StopAsync/DisposeAsync eventually
            // runs still drives the real teardown (reset/stop/lease release) exactly as for any
            // other fault.
            FaultFromPumpThread(ex);
            return false;
        }
    }

    /// <summary>
    ///     Faults this session's state and result buffer from the pump thread itself,
    ///     without starting teardown: unlike <see cref="FaultSession"/>, this is called while the
    ///     pump thread is still executing, so awaiting <see cref="_pumpTask"/>/
    ///     <see cref="_pumpRawCompletion"/> here (as starting teardown would) would await this very
    ///     thread. The pump thread is about to exit on its own right after this returns; the real
    ///     teardown (backend reset, device stop, lease release) still runs normally whenever a
    ///     caller eventually calls <see cref="StopAsync"/> or <see cref="DisposeAsync"/>.
    /// </summary>
    private void FaultFromPumpThread(Exception cause)
    {
        SessionStateChangedEventArgs? faultedArgs = null;
        lock (_syncRoot)
        {
            if (_state is RecognitionSessionState.Starting or RecognitionSessionState.Running or RecognitionSessionState.Stopping)
            {
                faultedArgs = TransitionTo(RecognitionSessionState.Faulted);
            }
        }

        // Raised only after _syncRoot has been released: see StartAsync's remarks.
        if (faultedArgs is not null)
        {
            RaiseStateChanged(faultedArgs);
        }

        _resultBuffer.Fault(cause);

        // Nothing reads the queue once the pump exits; completing it releases a file source
        // blocked on backpressure so StartAsync cannot hang.
        _pendingFrames?.Writer.TryComplete();
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
