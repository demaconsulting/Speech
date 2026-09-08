using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Real <see cref="ISpeechSynthesizer"/> implementation that chunks text into
///     <see cref="SpeechSegment"/>s, synthesizes each one on a background task while an earlier
///     one plays, and plays the resulting audio through a playback device.
/// </summary>
/// <remarks>
///     The pipeline mirrors <see cref="RecognitionSubsystem.SherpaOnnxSpeechRecognizer"/> but runs
///     in the opposite direction: rather than a native audio callback thread producing captured
///     blocks for a background consumer to recognize, a background producer task synthesizes
///     ordered <see cref="SynthesizedSpeech"/> segments into a bounded channel while the awaiting
///     caller (or <see cref="PlayStreamAsync"/>) plays them in order, so synthesis of a later
///     segment overlaps with playback of an earlier one per this library's chunked,
///     low-latency streaming design.
///     <para>
///     Unlike the recognizer's capture queue, the channel here never drops a segment: synthesized
///     audio that has already cost real inference time must never be silently discarded, so the
///     channel is bounded only to cap look-ahead memory and blocks the producer (rather than
///     dropping) when the consumer falls behind.
///     </para>
///     <para>
///     <see cref="Stop"/> cancels the in-flight session deterministically; a playback device
///     failure or engine failure fails the session's task honestly rather than hanging or
///     crashing, and <see cref="PlayStreamAsync"/> always stops the playback device in a
///     <c>finally</c> block so a fault never leaves it running.
///     </para>
/// </remarks>
internal sealed class SherpaOnnxSpeechSynthesizer : ISpeechSynthesizer
{
    /// <summary>
    ///     The maximum number of synthesized segments held pending playback before the producer
    ///     blocks. Raised from <c>2</c> to <c>5</c> to smooth pacing over long multi-sentence
    ///     text: with only <c>2</c>, playback could catch up to and stall on a still-synthesizing
    ///     segment whenever one chunk took noticeably longer to synthesize than its predecessor
    ///     took to play, even though the pipeline as a whole was keeping up on average. A capacity
    ///     of <c>5</c> gives the strictly sequential background producer more chunks of slack to
    ///     stay ahead of playback, at the cost of a few more segments' worth of look-ahead memory
    ///     - still small enough to bound both memory and latency to a handful of segments. This is
    ///     purely a buffer-size tuning change: it does not reduce the latency before the very
    ///     first word is spoken, which remains bounded by however long the first chunk alone
    ///     takes to synthesize.
    /// </summary>
    private const int PendingSegmentCapacity = 5;

    /// <summary>The diagnostics category used for every event this synthesizer reports.</summary>
    private const string DiagnosticsCategory = "SynthesisSubsystem";

    /// <summary>
    ///     How often <see cref="WaitForPlaybackDrainAsync"/> polls
    ///     <see cref="IAudioPlaybackDevice.PendingSampleCount"/> while waiting for the playback
    ///     device to finish rendering every queued sample. Short enough that playback never lags
    ///     noticeably behind the hardware actually finishing, long enough to avoid busy-spinning
    ///     the thread pool on a value that only a real-time audio callback can change.
    /// </summary>
    private static readonly TimeSpan DrainPollInterval = TimeSpan.FromMilliseconds(15);

    /// <summary>
    ///     An additional wait applied once <see cref="IAudioPlaybackDevice.PendingSampleCount"/>
    ///     first reports <c>0</c>, before <see cref="PlayStreamAsync"/> stops the device. Real
    ///     playback backends (notably PortAudio) may still hold a small amount of audio in an
    ///     internal host buffer that has already left the managed sample queue but has not yet
    ///     actually reached the speakers; this margin - roughly two callback buffers' worth at a
    ///     conservative estimate - covers that residual latency so the tail of the last segment
    ///     is not audibly clipped.
    /// </summary>
    private static readonly TimeSpan DrainTailMargin = TimeSpan.FromMilliseconds(40);

    /// <summary>The synthesis engine this synthesizer owns, uses, and disposes.</summary>
    private readonly ISynthesisEngine _engine;

    /// <summary>The available playback device to play synthesized audio through.</summary>
    private readonly IAudioPlaybackDevice _playbackDevice;

    /// <summary>The model driving text normalization, tag rendering, and parameter conventions.</summary>
    private readonly ISynthesisModel _model;

    /// <summary>
    ///     The session-level parameter value bag (for example a selected voice), or
    ///     <see langword="null"/> when the caller supplied none. Independent of, and never
    ///     conflated with, a segment's own per-tag <see cref="SpeechSegment.ParameterOverrides"/>
    ///     - see <see cref="GenerateSegment"/>'s remarks.
    /// </summary>
    private readonly IReadOnlyDictionary<string, object>? _parameterValues;

    /// <summary>The sink for structural lifecycle and fault events.</summary>
    private readonly ISpeechDiagnostics _diagnostics;

    /// <summary>Guards <see cref="_sessionCancellation"/> across concurrent Stop/SpeakAsync calls.</summary>
    private readonly object _syncRoot = new();

    /// <summary>The cancellation source for the currently in-flight session, if any.</summary>
    private CancellationTokenSource? _sessionCancellation;

    /// <summary>Whether <see cref="Dispose"/> has already run.</summary>
    private bool _isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SherpaOnnxSpeechSynthesizer"/> class over
    ///     an already-loaded engine and an available playback device.
    /// </summary>
    /// <param name="engine">The loaded synthesis engine this synthesizer owns and disposes. Must not be null.</param>
    /// <param name="playbackDevice">
    ///     The available playback device to play synthesized audio through. Must not be null and
    ///     must report <c>IsAvailable</c> as <see langword="true"/>;
    ///     <see cref="SpeechSynthesizerFactory"/> guarantees this.
    /// </param>
    /// <param name="model">The model to normalize text and render tags with. Must not be null.</param>
    /// <param name="parameterValues">
    ///     The session-level parameter value bag (for example a selected voice) to resolve a
    ///     speaker id from once per synthesized segment, or <see langword="null"/> when the
    ///     caller supplied none.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink for structural lifecycle and fault events, or <see langword="null"/> to use
    ///     <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="engine"/>, <paramref name="playbackDevice"/>, or
    ///     <paramref name="model"/> is null.
    /// </exception>
    internal SherpaOnnxSpeechSynthesizer(
        ISynthesisEngine engine,
        IAudioPlaybackDevice playbackDevice,
        ISynthesisModel model,
        IReadOnlyDictionary<string, object>? parameterValues = null,
        ISpeechDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(playbackDevice);
        ArgumentNullException.ThrowIfNull(model);

        _engine = engine;
        _playbackDevice = playbackDevice;
        _model = model;
        _parameterValues = parameterValues;
        _diagnostics = diagnostics ?? NullSpeechDiagnostics.Instance;
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <see langword="true"/>: this type is only ever created by
    ///     <see cref="SpeechSynthesizerFactory"/> after the engine loaded successfully and the
    ///     playback device reported itself available, so its existence is itself the
    ///     availability guarantee. Every unavailable case is represented by
    ///     <see cref="UnavailableSpeechSynthesizer"/> instead.
    /// </remarks>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public IAsyncEnumerable<SynthesizedSpeech> SynthesizeStreamAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        return SynthesizeStreamCore(text, cancellationToken);
    }

    /// <summary>
    ///     The iterator body of <see cref="SynthesizeStreamAsync"/>, split out so parameter
    ///     validation happens eagerly rather than only on the first enumeration.
    /// </summary>
    /// <param name="text">The already-validated text to synthesize.</param>
    /// <param name="cancellationToken">A token to cancel the session.</param>
    private async IAsyncEnumerable<SynthesizedSpeech> SynthesizeStreamCore(
        string text,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var normalized = _model.NormalizeText(text);
        var spans = AudioTagParser.Parse(normalized);
        var plan = _model.CapabilityProfile.Render(spans, _model);

        var channel = Channel.CreateBounded<SynthesizedSpeech>(
            new BoundedChannelOptions(PendingSegmentCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });

        var producerTask = Task.Run(() => ProduceAsync(plan, channel.Writer, cancellationToken), CancellationToken.None);

        try
        {
            await foreach (var segment in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return segment;
            }
        }
        finally
        {
            // The loop above can exit early via an exception (most commonly the reader observing
            // cancellationToken cancellation and throwing OperationCanceledException) before ever
            // reaching a normal-completion await. producerTask must still be awaited on every
            // exit path - normal completion, cancellation, or any other exception - because
            // ProduceAsync may be mid-way through a native engine call (GenerateSegment) when
            // cancellation fires: it only checks the token between segments, so the native call
            // can keep running, untracked, after this method has otherwise returned control to
            // its caller. If that caller then disposes the engine (as SpeakAsync's caller
            // commonly does once cancellation propagates), the still-running native call touches
            // freed native memory. Awaiting here unconditionally guarantees the producer has
            // genuinely finished before this iterator ever yields control past this point, which
            // also preserves the previous behavior of surfacing any genuine (non-cancellation)
            // producer fault to the caller on the normal-completion path, since ProduceAsync
            // completes the channel with that fault and this await then rethrows it.
            //
            // ProduceAsync itself swallows OperationCanceledException internally (see its own
            // catch block) and completes the channel normally instead of faulting on that path,
            // so this await will not normally throw OperationCanceledException; the catch below
            // only guards the unlikely case of a residual OperationCanceledException still
            // escaping, which is expected and benign here and must not mask whatever exception
            // (if any) is already propagating out of the try block above.
            try
            {
                await producerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Benign on the cancellation path; do not let it mask another exception already
                // propagating from the try block above.
            }
        }
    }

    /// <inheritdoc/>
    public async Task PlayStreamAsync(IAsyncEnumerable<SynthesizedSpeech> stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        try
        {
            // Started inside the try block (rather than before it) so that a failure from
            // Start() itself still runs the finally block below - otherwise a device that faults
            // on Start() would never reach Stop(), leaking whatever partial resource it acquired.
            _playbackDevice.Start();

            var resampler = new PlaybackAudioResampler(
                _engine.SampleRate,
                _playbackDevice.SampleRate > 0 ? _playbackDevice.SampleRate : _engine.SampleRate,
                _playbackDevice.ChannelCount > 0 ? _playbackDevice.ChannelCount : 1);

            await foreach (var segment in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                PlaySegment(segment, resampler);
            }

            // Every segment has been enqueued, but Write() is fire-and-forget: the playback
            // hardware may not have actually rendered any of it yet. Wait for genuine drain
            // before falling into the finally block's Stop(), which would otherwise discard
            // whatever is still queued and cut the audio off almost as soon as it started.
            await WaitForPlaybackDrainAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                _playbackDevice.Stop();
            }
            catch (Exception ex)
            {
                // Stopping the device is best-effort during teardown: a fault here must not mask
                // an earlier, more meaningful exception from playback itself.
                _diagnostics.Report(
                    SpeechDiagnosticLevel.Error,
                    DiagnosticsCategory,
                    $"Failed to stop the playback device after synthesis: {ex.Message}");
            }
        }
    }

    /// <inheritdoc/>
    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var session = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_syncRoot)
        {
            _sessionCancellation = session;
        }

        try
        {
            var stream = SynthesizeStreamAsync(text, session.Token);
            await PlayStreamAsync(stream, session.Token).ConfigureAwait(false);
        }
        finally
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_sessionCancellation, session))
                {
                    _sessionCancellation = null;
                }
            }

            session.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        CancellationTokenSource? session;
        lock (_syncRoot)
        {
            session = _sessionCancellation;
        }

        session?.Cancel();
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Stops any in-flight session and disposes the owned engine. Idempotent: a second call
    ///     does nothing, so a host may safely dispose a synthesizer it has already disposed.
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

        Stop();
        _engine.Dispose();
    }

    /// <summary>
    ///     Synthesizes every segment of a plan in order, writing each result to the channel, and
    ///     completes the channel normally on cancellation or with the fault on any other failure.
    /// </summary>
    /// <param name="plan">The rendered plan to synthesize.</param>
    /// <param name="writer">The channel to write completed segments to.</param>
    /// <param name="cancellationToken">A token to cancel the session.</param>
    private async Task ProduceAsync(SpeechPlan plan, ChannelWriter<SynthesizedSpeech> writer, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var segment in plan.Segments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var synthesized = GenerateSegment(segment);
                await writer.WriteAsync(synthesized, cancellationToken).ConfigureAwait(false);
            }

            writer.TryComplete();
        }
        catch (OperationCanceledException)
        {
            writer.TryComplete();
        }
        catch (Exception ex)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"Speech synthesis stopped because a segment could not be synthesized: {ex.Message}");
            writer.TryComplete(ex);
        }
    }

    /// <summary>
    ///     Synthesizes one segment, applying any speed/volume parameter overrides it carries via
    ///     <see cref="SpeechParameterConventions"/>, resolving the session-level speaker id via
    ///     <see cref="ISynthesisModel.ResolveSpeakerId"/>, or produces pure silence for an
    ///     empty-text pause segment without calling the engine at all.
    /// </summary>
    /// <param name="segment">The segment to synthesize.</param>
    /// <returns>The resulting <see cref="SynthesizedSpeech"/>.</returns>
    /// <remarks>
    ///     Voice selection (<see cref="_parameterValues"/>, resolved via
    ///     <see cref="ISynthesisModel.ResolveSpeakerId"/>) and this segment's own per-tag
    ///     <see cref="SpeechSegment.ParameterOverrides"/> (resolved via
    ///     <see cref="ResolveOverrideRatios"/>) are two independent mechanisms with different
    ///     lifetimes - a session-level voice choice made once per synthesizer versus a
    ///     transient, per-segment Natural Language Audio Tag override - and this method combines
    ///     them without either one influencing the other: the resolved speaker id is
    ///     re-evaluated (cheaply; the hook is pure) for every segment rather than cached once for
    ///     the whole session, so it always reflects <see cref="_parameterValues"/> exactly, while
    ///     the speed/volume ratios continue to come solely from this segment's own overrides.
    /// </remarks>
    private SynthesizedSpeech GenerateSegment(SpeechSegment segment)
    {
        if (segment.Text.Length == 0)
        {
            return new SynthesizedSpeech(
                [],
                _engine.SampleRate,
                TimeSpan.FromMilliseconds(segment.PreSilenceMs),
                TimeSpan.FromMilliseconds(segment.PostSilenceMs));
        }

        var (speedRatio, volumeRatio) = ResolveOverrideRatios(segment.ParameterOverrides);
        var speakerId = _model.ResolveSpeakerId(_parameterValues);

        var generated = _engine.Generate(segment.Text, speedRatio, speakerId);
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
    ///     Resolves a segment's parameter overrides into an engine speed ratio and a post-hoc
    ///     volume (amplitude) ratio, by matching each overridden parameter id against
    ///     <see cref="SpeechParameterConventions"/> and this model's own declared parameters.
    /// </summary>
    /// <param name="overrides">The segment's parameter overrides, or <see langword="null"/>.</param>
    /// <returns>The speed ratio (default <c>1.0f</c>) and volume ratio (default <c>1.0</c>) to apply.</returns>
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
            if (parameter is null || parameter.Default == 0 || overriddenValue is not double doubleValue)
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
    /// <param name="samples">The samples to scale.</param>
    /// <param name="ratio">The amplitude ratio to apply.</param>
    /// <returns>A newly allocated, scaled sample array.</returns>
    private static float[] ApplyVolume(float[] samples, double ratio)
    {
        var scaled = new float[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            scaled[i] = Math.Clamp((float)(samples[i] * ratio), -1.0f, 1.0f);
        }

        return scaled;
    }

    /// <summary>
    ///     Polls <see cref="IAudioPlaybackDevice.PendingSampleCount"/> until every sample written
    ///     during this session has genuinely been rendered by the playback hardware (not merely
    ///     enqueued), then applies a small additional tail wait for residual host buffering.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A token that, when cancelled, ends the wait promptly rather than waiting for the full
    ///     drain, consistent with cancellation elsewhere in this class.
    /// </param>
    /// <remarks>
    ///     Polls rather than busy-spins because <see cref="IAudioPlaybackDevice.PendingSampleCount"/>
    ///     only changes when PortAudio's own real-time callback thread dequeues samples - there is
    ///     nothing productive this thread can do except wait for that to happen.
    /// </remarks>
    private async Task WaitForPlaybackDrainAsync(CancellationToken cancellationToken)
    {
        while (_playbackDevice.PendingSampleCount > 0)
        {
            await Task.Delay(DrainPollInterval, cancellationToken).ConfigureAwait(false);
        }

        await Task.Delay(DrainTailMargin, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Writes one segment's pre-silence, resampled audio, and post-silence to the playback
    ///     device in order.
    /// </summary>
    /// <param name="segment">The segment to play.</param>
    /// <param name="resampler">The conversion from the engine's rate to the device's resolved format.</param>
    private void PlaySegment(SynthesizedSpeech segment, PlaybackAudioResampler resampler)
    {
        WriteSilence(segment.PreSilence);

        if (segment.Samples.Count > 0)
        {
            var interleaved = resampler.Convert(segment.Samples is float[] array ? array : [.. segment.Samples]);
            if (interleaved.Length > 0)
            {
                _playbackDevice.Write(interleaved);
            }
        }

        WriteSilence(segment.PostSilence);
    }

    /// <summary>
    ///     Writes a block of zero-valued samples to the playback device representing a duration
    ///     of real silence, sized for the device's resolved sample rate and channel count.
    /// </summary>
    /// <param name="duration">The duration of silence to write.</param>
    private void WriteSilence(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        var sampleRate = _playbackDevice.SampleRate > 0 ? _playbackDevice.SampleRate : _engine.SampleRate;
        var channelCount = _playbackDevice.ChannelCount > 0 ? _playbackDevice.ChannelCount : 1;
        var frameCount = (int)(duration.TotalSeconds * sampleRate);
        if (frameCount <= 0)
        {
            return;
        }

        _playbackDevice.Write(new float[frameCount * channelCount]);
    }
}
