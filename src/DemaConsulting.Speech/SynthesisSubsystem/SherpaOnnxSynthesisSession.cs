using System.Numerics.Tensors;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Real <see cref="ISynthesisSession"/> implementation that chunks text into
///     <see cref="SpeechSegment"/>s, synthesizes each one on a dedicated worker thread (bounding
///     how long a non-cooperative native call is waited on), and - for <see cref="SpeakAsync"/> -
///     plays the resulting audio through this session's bound playback device.
/// </summary>
/// <remarks>
///     Moved here from the former <c>SherpaOnnxSpeechSynthesizer</c>: the chunking/pipelined-
///     generation logic and playback-device ownership are unchanged in substance, now wrapped in
///     an explicit state machine (<see cref="SynthesisSessionState"/>), an overlap guard (at most
///     one <see cref="SpeakAsync"/>/<see cref="SynthesizeAsync"/> call in flight at a time), and
///     per-segment native calls routed through <see cref="DedicatedWorker"/> for the
///     cooperative-cancel-then-abandon policy.
///     <para>
///     Unlike the former streaming pipeline, this session does not pipeline synthesis ahead of
///     playback across an unbounded channel: each segment is synthesized, then (for
///     <see cref="SpeakAsync"/>) written to the playback device, before the next segment's
///     synthesis begins. This keeps the per-call state machine simple (one worker call in flight
///     at a time) while still overlapping this call's own synthesis-then-playback work normally.
///     </para>
/// </remarks>
internal sealed class SherpaOnnxSynthesisSession : ISynthesisSession
{
    /// <summary>The diagnostics category used for every event this session reports.</summary>
    private const string DiagnosticsCategory = "SynthesisSubsystem";

    /// <summary>
    ///     How often <see cref="WaitForPlaybackDrainAsync"/> polls
    ///     <see cref="IAudioPlaybackDevice.PendingSampleCount"/> while waiting for the playback
    ///     device to finish rendering every queued sample.
    /// </summary>
    private static readonly TimeSpan DrainPollInterval = TimeSpan.FromMilliseconds(15);

    /// <summary>
    ///     An additional wait applied once <see cref="IAudioPlaybackDevice.PendingSampleCount"/>
    ///     first reports <c>0</c>, before playback is considered drained, covering a real
    ///     playback backend's residual host-buffer latency.
    /// </summary>
    private static readonly TimeSpan DrainTailMargin = TimeSpan.FromMilliseconds(40);

    /// <summary>The synthesis backend this session uses for every native call.</summary>
    private readonly ISynthesisBackend _backend;

    /// <summary>The playback device this session is bound to for its entire life.</summary>
    private readonly IAudioPlaybackDevice _device;

    /// <summary>The model driving text normalization, tag rendering, and parameter conventions.</summary>
    private readonly ISynthesisModel _model;

    /// <summary>The session-level parameter value bag, or <see langword="null"/> when none was supplied.</summary>
    private readonly IReadOnlyDictionary<string, object>? _parameterValues;

    /// <summary>The sink for structural lifecycle and fault events.</summary>
    private readonly ISpeechDiagnostics _diagnostics;

    /// <summary>Invoked exactly once, from <see cref="DisposeAsync"/>, to release the engine's exclusivity lease.</summary>
    private readonly Action _releaseLease;

    /// <summary>Guards every field below against concurrent <see cref="SpeakAsync"/>/<see cref="SynthesizeAsync"/>/<see cref="StopAsync"/>/<see cref="DisposeAsync"/> calls.</summary>
    private readonly object _syncRoot = new();

    /// <summary>This session's current lifecycle state.</summary>
    private SynthesisSessionState _state = SynthesisSessionState.Created;

    /// <summary>The cancellation source for the currently in-flight operation, if any.</summary>
    private CancellationTokenSource? _operationCancellation;

    /// <summary>
    ///     The <see cref="Task"/> backing the currently in-flight <see cref="SpeakAsync"/>/
    ///     <see cref="SynthesizeAsync"/> call, if any. Tracked so <see cref="StopAsync"/> and
    ///     <see cref="DisposeAsync"/> can await genuine completion of the operation - including
    ///     the underlying <see cref="DedicatedWorker"/> call it may still be waiting on - rather
    ///     than merely requesting cancellation and returning while native work is still running.
    /// </summary>
    private Task? _operationTask;

    /// <summary>
    ///     The dedicated worker thread's own raw completion for the most recently started native
    ///     <c>Generate</c> call, set alongside (but independently of) <see cref="_operationTask"/>.
    ///     Unlike <see cref="_operationTask"/>, this completes only once the native call has
    ///     genuinely returned - even if the operation's abandon-aware task already completed early
    ///     as abandoned - so <see cref="DisposeAsync"/> can await it before releasing the engine's
    ///     exclusivity lease, closing the race where an abandoned native call could still be
    ///     running against a backend a new session (or engine disposal) is now free to touch.
    /// </summary>
    private Task? _pendingNativeCompletion;

    /// <summary>The exception that faulted this session, if <see cref="_state"/> is <see cref="SynthesisSessionState.Faulted"/>.</summary>
    private Exception? _fault;

    /// <summary>
    ///     The single-flight disposal operation, shared by every concurrent <see cref="DisposeAsync"/>
    ///     caller so a second call awaits the same real teardown rather than returning as soon as
    ///     the first call merely begins. Also doubles as the disposed flag: non-null means
    ///     disposal has started.
    /// </summary>
    private Task? _disposeTask;

    /// <summary>Whether the exclusivity lease has already been released.</summary>
    private bool _leaseReleased;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SherpaOnnxSynthesisSession"/> class, bound
    ///     to one playback device for its entire life.
    /// </summary>
    /// <param name="backend">The loaded synthesis backend to synthesize with. Must not be null.</param>
    /// <param name="device">The playback device this session is bound to. Must not be null.</param>
    /// <param name="model">The model to normalize text and render tags with. Must not be null.</param>
    /// <param name="parameterValues">The session-level parameter value bag, or <see langword="null"/>.</param>
    /// <param name="diagnostics">The sink for structural lifecycle and fault events. Must not be null.</param>
    /// <param name="releaseLease">Invoked exactly once, from <see cref="DisposeAsync"/>, to release the engine's exclusivity lease.</param>
    internal SherpaOnnxSynthesisSession(
        ISynthesisBackend backend,
        IAudioPlaybackDevice device,
        ISynthesisModel model,
        IReadOnlyDictionary<string, object>? parameterValues,
        ISpeechDiagnostics diagnostics,
        Action releaseLease)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(releaseLease);

        _backend = backend;
        _device = device;
        _model = model;
        _parameterValues = parameterValues;
        _diagnostics = diagnostics;
        _releaseLease = releaseLease;
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     <see langword="false"/> once this session has been disposed or has faulted; otherwise
    ///     always <see langword="true"/> - this type is only ever constructed with a real, loaded
    ///     backend by <see cref="SherpaOnnxSpeechSynthesizerEngine"/>.
    /// </remarks>
    public bool IsAvailable
    {
        get
        {
            lock (_syncRoot)
            {
                return _disposeTask is null && _state != SynthesisSessionState.Faulted;
            }
        }
    }

    /// <inheritdoc/>
    public SynthesisSessionState State
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
    public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return StartOperation(text, playAfterSynthesis: true, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<SynthesizedSpeech>> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return StartOperation(text, playAfterSynthesis: false, cancellationToken);
    }

    /// <summary>
    ///     Validates the overlap rule and current state, then starts the operation and records it
    ///     as this session's in-flight operation (see <see cref="_operationTask"/>) atomically
    ///     with that validation.
    /// </summary>
    /// <remarks>
    ///     Validation and registration both run inside one <see cref="_syncRoot"/> critical
    ///     section, including the call into <see cref="RunOperationAsync"/> itself: <c>lock</c> is
    ///     reentrant on the same thread, and calling an <see langword="async"/> method runs it
    ///     synchronously up to its first genuine <see langword="await"/> before control returns
    ///     here with a <see cref="Task"/> handle - so <see cref="RunOperationAsync"/>'s own state
    ///     transitions to <see cref="SynthesisSessionState.Running"/> have already happened by the
    ///     time <see cref="_operationTask"/> is assigned below. This closes the window where a
    ///     concurrent <see cref="DisposeAsync"/> could otherwise observe <see cref="_operationTask"/>
    ///     as <see langword="null"/> while the operation is already inside
    ///     <see cref="GenerateSegmentAsync"/>/native <c>Generate</c>.
    /// </remarks>
    private Task<IReadOnlyList<SynthesizedSpeech>> StartOperation(
        string text,
        bool playAfterSynthesis,
        CancellationToken callerToken)
    {
        var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        lock (_syncRoot)
        {
            if (_disposeTask is not null)
            {
                operationCancellation.Dispose();
                throw new ObjectDisposedException(nameof(SherpaOnnxSynthesisSession));
            }

            if (_state == SynthesisSessionState.Faulted)
            {
                operationCancellation.Dispose();
                throw new SynthesisSessionFaultedException(
                    "Cannot perform a synthesis operation: this session has faulted.", _fault!);
            }

            if (_state is SynthesisSessionState.Starting or SynthesisSessionState.Running or SynthesisSessionState.Stopping)
            {
                operationCancellation.Dispose();
                throw new InvalidOperationException(
                    "session already has an operation in progress; await completion before calling again");
            }

            _operationCancellation = operationCancellation;

            var task = RunOperationAsync(text, playAfterSynthesis, operationCancellation);
            _operationTask = task;
            return task;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Requests cancellation of any in-flight operation and awaits the tracked operation's own
    ///     completion, including its raw native-call completion (see
    ///     <see cref="_pendingNativeCompletion"/>) so an abandoned <see cref="DedicatedWorker"/>
    ///     native call cannot still be running once this returns (finding 27), before returning,
    ///     matching the documented <see cref="ISynthesisSession.StopAsync"/> contract that the
    ///     returned task completes once the in-flight operation has genuinely stopped.
    ///     <paramref name="cancellationToken"/> bounds only this caller's own wait for that
    ///     teardown (finding 28), mirroring the recognition session's <c>StopAsync</c>: it never
    ///     aborts the underlying cancel-and-await work itself, which every other concurrent caller
    ///     (and <see cref="DisposeAsync"/>) still needs to complete regardless of whether this
    ///     particular caller stopped waiting for it.
    /// </remarks>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? operationCancellation;
        Task? operationTask;
        Task? pendingNativeCompletion;
        lock (_syncRoot)
        {
            operationCancellation = _operationCancellation;
            operationTask = _operationTask;
            pendingNativeCompletion = _pendingNativeCompletion;
        }

        var stopTask = CancelAndAwaitOperationAsync(operationCancellation, operationTask, pendingNativeCompletion);

        return cancellationToken.CanBeCanceled
            ? stopTask.WaitAsync(cancellationToken)
            : stopTask;
    }

    /// <summary>
    ///     Requests cancellation of <paramref name="operationCancellation"/> (if any), tolerating
    ///     the case where the in-flight operation has already completed and disposed it
    ///     concurrently.
    /// </summary>
    private static async Task CancelOperationAsync(CancellationTokenSource? operationCancellation)
    {
        try
        {
            if (operationCancellation is not null)
            {
                await operationCancellation.CancelAsync().ConfigureAwait(false);
            }
        }
        catch (ObjectDisposedException)
        {
            // The in-flight operation completed and disposed its own cancellation source
            // concurrently with this call; there is nothing left to cancel.
        }
    }

    /// <summary>
    ///     Requests cancellation of <paramref name="operationCancellation"/> (if any) and then
    ///     awaits <paramref name="operationTask"/> (if any) and, when supplied,
    ///     <paramref name="pendingNativeCompletion"/> to genuine completion.
    /// </summary>
    /// <param name="operationCancellation">The in-flight operation's cancellation source, if any.</param>
    /// <param name="operationTask">The in-flight operation's abandon-aware task, if any.</param>
    /// <param name="pendingNativeCompletion">
    ///     The in-flight operation's raw native-call completion (see <see cref="_pendingNativeCompletion"/>),
    ///     if any. Both <see cref="StopAsync"/> and <see cref="DisposeAsync"/> supply this: an
    ///     abandoned <see cref="DedicatedWorker"/> native call must have genuinely returned before
    ///     either is allowed to report the operation as stopped, since <see cref="StopAsync"/>'s
    ///     documented contract requires genuine quiescence and <see cref="DisposeAsync"/> must not
    ///     release the engine's exclusivity lease while it may still be running.
    /// </param>
    private static async Task CancelAndAwaitOperationAsync(
        CancellationTokenSource? operationCancellation,
        Task? operationTask,
        Task? pendingNativeCompletion = null)
    {
        await CancelOperationAsync(operationCancellation).ConfigureAwait(false);

        if (operationTask is not null)
        {
            try
            {
                await operationTask.ConfigureAwait(false);
            }
            catch
            {
                // RunOperationAsync already transitions state and reports diagnostics for its own
                // failure/cancellation; the caller here only needs to know the operation has
                // actually finished.
            }
        }

        if (pendingNativeCompletion is not null)
        {
            try
            {
                await pendingNativeCompletion.ConfigureAwait(false);
            }
            catch
            {
                // Already reported (if it genuinely faulted) by GenerateSegmentAsync's own
                // caller; this await exists purely to prove the native call has genuinely
                // returned, not to re-surface its outcome.
            }
        }
    }

    /// <summary>
    ///     Runs one <see cref="SpeakAsync"/>/<see cref="SynthesizeAsync"/> operation end-to-end:
    ///     transitions through <see cref="SynthesisSessionState.Starting"/>/<see cref="SynthesisSessionState.Running"/>,
    ///     synthesizes (and optionally plays) every segment, then transitions through
    ///     <see cref="SynthesisSessionState.Stopping"/> back to <see cref="SynthesisSessionState.Stopped"/>
    ///     on success or genuine, promptly-honored cancellation, or to
    ///     <see cref="SynthesisSessionState.Faulted"/> on any other failure - including a
    ///     cancellation request the native <c>Generate</c> call did not honor within
    ///     <see cref="DedicatedWorker"/>'s abandon timeout (finding 26), since that leaves the
    ///     shared backend not safely reusable until the abandoned call genuinely returns.
    /// </summary>
    /// <param name="text">The text to synthesize.</param>
    /// <param name="playAfterSynthesis">Whether to play each segment through the bound device as it is produced.</param>
    /// <param name="operationCancellation">
    ///     This operation's cancellation source, already recorded as <see cref="_operationCancellation"/>
    ///     by <see cref="StartOperation"/> before this method runs.
    /// </param>
    /// <returns>The ordered synthesized segments.</returns>
    private async Task<IReadOnlyList<SynthesizedSpeech>> RunOperationAsync(
        string text,
        bool playAfterSynthesis,
        CancellationTokenSource operationCancellation)
    {
        TransitionTo(SynthesisSessionState.Starting);
        TransitionTo(SynthesisSessionState.Running);

        try
        {
            var results = await GenerateAndOptionallyPlayAsync(text, playAfterSynthesis, operationCancellation.Token)
                .ConfigureAwait(false);

            ClearOperation();

            TransitionTo(SynthesisSessionState.Stopping);
            TransitionTo(SynthesisSessionState.Stopped);
            return results;
        }
        catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
        {
            // Only an exception that corresponds to this operation's own cancellation request
            // (from a caller's token, StopAsync, or DisposeAsync - all of which cancel this same
            // linked source) is even a candidate for normal cancellation. An
            // OperationCanceledException the backend throws on its own initiative, with no
            // cancellation actually requested, falls through to the general fault handler below
            // instead (finding 9), since silently treating it as a clean stop would let a
            // genuinely broken backend be reused.
            Task? pendingNativeCompletion;
            lock (_syncRoot)
            {
                pendingNativeCompletion = _pendingNativeCompletion;
            }

            if (pendingNativeCompletion is not null && !pendingNativeCompletion.IsCompleted)
            {
                // The native Generate call did not honor this request within DedicatedWorker's
                // abandon timeout and is still running in the background (finding 26): the shared
                // backend is not safely reusable until that raw completion genuinely finishes, so
                // - unlike a native call that stopped promptly - this is not a clean cancellation
                // this session can return to Stopped from. Fault instead, which both reports the
                // condition and (via StartOperation's Faulted check) keeps this session from
                // starting a second native call concurrently with the still-running abandoned
                // one; _pendingNativeCompletion itself is left set so StopAsync/DisposeAsync still
                // await its genuine completion.
                var abandonFault = new TimeoutException(
                    "A native Generate call did not honor a cancellation request within the " +
                    "dedicated worker's abandon timeout and was abandoned while still running.");

                lock (_syncRoot)
                {
                    _operationCancellation = null;
                    _operationTask = null;
                    _fault = abandonFault;
                }

                TransitionTo(SynthesisSessionState.Faulted);

                _diagnostics.Report(
                    SpeechDiagnosticLevel.Error,
                    DiagnosticsCategory,
                    $"Synthesis session faulted: {abandonFault.Message}");
                throw;
            }

            ClearOperation();

            TransitionTo(SynthesisSessionState.Stopping);
            TransitionTo(SynthesisSessionState.Stopped);
            throw;
        }
        catch (Exception ex)
        {
            lock (_syncRoot)
            {
                _operationCancellation = null;
                _operationTask = null;
                _fault = ex;
            }

            TransitionTo(SynthesisSessionState.Faulted);

            // Intentionally broad: a synthesis or playback fault must transition the session to
            // the terminal Faulted state and be reported as a structural fact rather than escape
            // unobserved.
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"Synthesis session faulted: {ex.Message}");
            throw;
        }
        finally
        {
            operationCancellation.Dispose();
        }
    }

    /// <summary>Clears <see cref="_operationCancellation"/> and <see cref="_operationTask"/> once an operation has settled.</summary>
    private void ClearOperation()
    {
        lock (_syncRoot)
        {
            _operationCancellation = null;
            _operationTask = null;
        }
    }

    /// <summary>
    ///     Synthesizes every segment of the rendered plan in order, writing each to the playback
    ///     device immediately when <paramref name="playAfterSynthesis"/> is <see langword="true"/>,
    ///     and waiting for genuine playback drain once every segment has been written.
    /// </summary>
    private async Task<IReadOnlyList<SynthesizedSpeech>> GenerateAndOptionallyPlayAsync(
        string text,
        bool playAfterSynthesis,
        CancellationToken cancellationToken)
    {
        var normalized = _model.NormalizeText(text);
        var spans = AudioTagParser.Parse(normalized);
        var plan = _model.CapabilityProfile.Render(spans, _model);

        List<SynthesizedSpeech> results = [];

        if (!playAfterSynthesis)
        {
            foreach (var segment in plan.Segments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(await GenerateSegmentAsync(segment, cancellationToken).ConfigureAwait(false));
            }

            return results;
        }

        try
        {
            // Run the native, potentially slow IAudioPlaybackDevice.Start() off this caller's
            // thread: this method is already async, but this was the first call in its body, so
            // without this it would still block the caller/UI thread for the entire native
            // device-open/start call before the method's first genuine await. A plain
            // Task.Run hand-off is sufficient here (unlike the per-segment native Generate call
            // below) - this is a one-shot startup step, not a per-call hot-loop operation, so the
            // cooperative-cancel-then-abandon machinery of DedicatedWorker is unnecessary
            // complexity that would risk new races for no benefit.
            await Task.Run(() => _device.Start(), CancellationToken.None).ConfigureAwait(false);

            var resampler = new PlaybackAudioResampler(
                _backend.SampleRate,
                _device.SampleRate > 0 ? _device.SampleRate : _backend.SampleRate,
                _device.ChannelCount > 0 ? _device.ChannelCount : 1);

            foreach (var segment in plan.Segments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var synthesized = await GenerateSegmentAsync(segment, cancellationToken).ConfigureAwait(false);
                results.Add(synthesized);
                PlaySegment(synthesized, resampler);
            }

            await WaitForPlaybackDrainAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                _device.Stop();
            }
            catch (Exception ex)
            {
                // Intentionally broad: stopping the device during teardown is best-effort against
                // the native playback backend, and a stop fault must not mask the earlier outcome.
                _diagnostics.Report(
                    SpeechDiagnosticLevel.Error,
                    DiagnosticsCategory,
                    $"Failed to stop the playback device after synthesis: {ex.Message}");
            }
        }

        return results;
    }

    /// <summary>
    ///     Synthesizes one segment, applying any speed/volume parameter overrides it carries,
    ///     resolving the session-level speaker id, or producing pure silence for an empty-text
    ///     pause segment without calling the backend at all. The backend call itself runs on a
    ///     <see cref="DedicatedWorker"/> so a non-cooperative native call is bounded by the
    ///     cooperative-cancel-then-abandon policy rather than awaited indefinitely.
    /// </summary>
    private async Task<SynthesizedSpeech> GenerateSegmentAsync(SpeechSegment segment, CancellationToken cancellationToken)
    {
        if (segment.Text.Length == 0)
        {
            return new SynthesizedSpeech(
                [],
                _backend.SampleRate,
                TimeSpan.FromMilliseconds(segment.PreSilenceMs),
                TimeSpan.FromMilliseconds(segment.PostSilenceMs));
        }

        var (speedRatio, volumeRatio) = ResolveOverrideRatios(segment.ParameterOverrides);
        var speakerId = _model.ResolveSpeakerId(_parameterValues);

        var run = DedicatedWorker.Start(
            _ => _backend.Generate(segment.Text, speedRatio, speakerId),
            cancellationToken,
            _diagnostics,
            DiagnosticsCategory);

        lock (_syncRoot)
        {
            _pendingNativeCompletion = run.Completion;
        }

        var generated = await run.Task.ConfigureAwait(false);

        var samples = generated.Samples;
        if (Math.Abs(volumeRatio - 1.0) > double.Epsilon)
        {
            samples = ApplyVolume(samples, volumeRatio);
        }

        return new SynthesizedSpeech(
            samples,
            generated.SampleRate,
            TimeSpan.FromMilliseconds(segment.PreSilenceMs),
            TimeSpan.FromMilliseconds(segment.PostSilenceMs));
    }

    /// <summary>
    ///     Resolves a segment's parameter overrides into a backend speed ratio and a post-hoc
    ///     volume (amplitude) ratio, by matching each overridden parameter id against
    ///     <see cref="SpeechParameterConventions"/>.
    /// </summary>
    private (float SpeedRatio, double VolumeRatio) ResolveOverrideRatios(IReadOnlyDictionary<string, object>? overrides)
    {
        var speedRatio = 1.0f;
        var volumeRatio = 1.0;
        if (overrides is null || overrides.Count == 0)
        {
            return (speedRatio, volumeRatio);
        }

        foreach (var (parameterId, overriddenValue) in overrides)
        {
            var parameter = _model.Parameters
                .OfType<NumericParameter>()
                .FirstOrDefault(candidate => candidate.Id == parameterId);
            if (parameter is null || Math.Abs(parameter.Default) <= double.Epsilon || overriddenValue is not double doubleValue)
            {
                continue;
            }

            var ratio = doubleValue / parameter.Default;
            if (SpeechParameterConventions.IsSpeedParameter(parameterId))
            {
                speedRatio = (float)ratio;
            }
            else if (SpeechParameterConventions.IsVolumeParameter(parameterId))
            {
                volumeRatio = ratio;
            }
        }

        return (speedRatio, volumeRatio);
    }

    /// <summary>
    ///     Scales sample amplitude by a ratio, clamping to <c>[-1.0, 1.0]</c> so a boosted segment
    ///     never clips into an invalid sample value.
    /// </summary>
    private static float[] ApplyVolume(float[] samples, double ratio)
    {
        var scaled = new float[samples.Length];
        var ratioF = (float)ratio;
        TensorPrimitives.Multiply(samples, ratioF, scaled);
        TensorPrimitives.Clamp(scaled, -1.0f, 1.0f, scaled);
        return scaled;
    }

    /// <summary>
    ///     Polls <see cref="IAudioPlaybackDevice.PendingSampleCount"/> until every sample written
    ///     during this operation has genuinely been rendered by the playback hardware, then
    ///     applies a small additional tail wait for residual host buffering.
    /// </summary>
    private async Task WaitForPlaybackDrainAsync(CancellationToken cancellationToken)
    {
        while (_device.PendingSampleCount > 0)
        {
            await Task.Delay(DrainPollInterval, cancellationToken).ConfigureAwait(false);
        }

        await Task.Delay(DrainTailMargin, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Writes one segment's pre-silence, resampled audio, and post-silence to the playback
    ///     device in order.
    /// </summary>
    private void PlaySegment(SynthesizedSpeech segment, PlaybackAudioResampler resampler)
    {
        WriteSilence(segment.PreSilence);

        if (segment.Samples.Count > 0)
        {
            var interleaved = resampler.Convert(segment.Samples is float[] array ? array : [.. segment.Samples]);
            if (interleaved.Length > 0)
            {
                _device.Write(interleaved);
            }
        }

        WriteSilence(segment.PostSilence);
    }

    /// <summary>
    ///     Writes a block of zero-valued samples to the playback device representing a duration
    ///     of real silence, sized for the device's resolved sample rate and channel count.
    /// </summary>
    private void WriteSilence(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        var sampleRate = _device.SampleRate > 0 ? _device.SampleRate : _backend.SampleRate;
        var channelCount = _device.ChannelCount > 0 ? _device.ChannelCount : 1;
        var frameCount = (int)(duration.TotalSeconds * sampleRate);
        if (frameCount <= 0)
        {
            return;
        }

        _device.Write(new float[frameCount * channelCount]);
    }

    /// <summary>
    ///     Updates <see cref="_state"/> and raises <see cref="StateChanged"/>, with handler
    ///     exceptions caught and routed to diagnostics rather than propagated, mirroring this
    ///     library's other event-exception-isolation conventions.
    /// </summary>
    private void TransitionTo(SynthesisSessionState newState)
    {
        SynthesisSessionState previous;
        lock (_syncRoot)
        {
            previous = _state;
            _state = newState;
        }

        if (previous == newState)
        {
            return;
        }

        try
        {
            StateChanged?.Invoke(this, new SessionStateChangedEventArgs(previous, newState));
        }
        catch (Exception ex)
        {
            // Intentionally broad: a host's StateChanged handler must never be able to destabilize
            // this session's own lifecycle.
            _diagnostics.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                $"A StateChanged handler threw: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Every concurrent caller shares the same single-flight disposal task rather than a
    ///     second call returning the instant the first merely begins: both wait for the exact
    ///     same cancellation, operation completion (including the raw native-call completion - see
    ///     <see cref="_pendingNativeCompletion"/> - so an abandoned <see cref="DedicatedWorker"/>
    ///     thread cannot still be touching the shared backend once this returns), state
    ///     transitions, and engine-lease release to complete.
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
    ///     Cancels and awaits any in-flight operation to genuine completion (including the raw
    ///     native-call completion), then transitions through <see cref="SynthesisSessionState.Disposing"/>
    ///     to <see cref="SynthesisSessionState.Disposed"/> and releases the engine's exclusivity
    ///     lease exactly once - only after the operation has settled, so the lease is never
    ///     released while this session's own call into the backend is still demonstrably in
    ///     flight, whether or not it was abandoned. Started at most once; see <see cref="_disposeTask"/>.
    /// </summary>
    private async Task DisposeCoreAsync()
    {
        CancellationTokenSource? operationCancellation;
        Task? operationTask;
        Task? pendingNativeCompletion;
        lock (_syncRoot)
        {
            operationCancellation = _operationCancellation;
            operationTask = _operationTask;
            pendingNativeCompletion = _pendingNativeCompletion;
        }

        await CancelAndAwaitOperationAsync(operationCancellation, operationTask, pendingNativeCompletion).ConfigureAwait(false);

        TransitionTo(SynthesisSessionState.Disposing);
        TransitionTo(SynthesisSessionState.Disposed);

        lock (_syncRoot)
        {
            if (_leaseReleased)
            {
                return;
            }

            _leaseReleased = true;
        }

        _releaseLease();
    }
}
