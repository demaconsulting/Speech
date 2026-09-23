using SherpaOnnx;

namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Real <see cref="IRecognitionEngine"/> implementation wrapping one sherpa-onnx
///     <see cref="OnlineRecognizer"/> and the <see cref="OnlineStream"/> that carries the current
///     utterance.
/// </summary>
/// <remarks>
///     This is the only type in the library that calls sherpa-onnx inference APIs, which is what
///     makes every other unit of the recognition subsystem testable without a native runtime.
///     It implements the engine's documented polling protocol: samples are pushed in with
///     <c>AcceptWaveform</c>, decoded while the recognizer reports the stream ready, and read
///     back as text; when the recognizer reports an endpoint, the utterance is emitted as a final
///     result and the stream is reset for the next one.
///     <para>
///     Construction loads the model into native memory and therefore fails (throws) when the
///     native runtime binary for the current RID is absent or the model files are unusable.
///     Callers convert that into the honest <see cref="UnavailableSpeechRecognizer"/> fallback;
///     see <see cref="SpeechRecognizerFactory"/>.
///     </para>
///     <para>
///     Instances are not thread-safe and own unmanaged resources: exactly one caller thread may
///     use an instance at a time, and it must be disposed.
///     </para>
///     <para>
///     Two distinct reset paths exist and are deliberately not unified: the endpoint path inside
///     <c>TryDecode</c> resets the existing stream in place (buffered pre/post-endpoint audio is
///     wanted there, for warm-up replay), while the session-end <see cref="Reset"/> - called when
///     a host stops a session, always preceded by <see cref="TryFlush"/> - discards the stream
///     entirely and creates a replacement, so any audio accepted but still not decoded even
///     after that flush cannot bleed into the next session's decoding.
///     </para>
/// </remarks>
internal sealed class SherpaOnnxRecognitionEngine : IRecognitionEngine
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SherpaOnnxRecognitionEngine"/> class,
    ///     loading the model described by a configuration into a native recognizer.
    /// </summary>
    /// <param name="config">
    ///     The model's own engine configuration, as produced by
    ///     <c>IRecognitionModel.CreateEngineConfig</c>.
    /// </param>
    /// <param name="sampleRate">
    ///     The rate, in Hz, at which samples passed to <see cref="AcceptSamples"/> are supplied.
    ///     Must be greater than zero and must match the model's declared rate.
    /// </param>
    /// <param name="postEndpointWarmupWindowMs">
    ///     The duration, in milliseconds, of pre-endpoint audio to buffer and silently replay
    ///     into a freshly reset stream after every endpoint, as declared by
    ///     <c>IRecognitionModel.PostEndpointWarmupWindowMs</c>. Must be greater than or equal to
    ///     zero. <c>0</c> (the default for every model except
    ///     <see cref="ModelManagementSubsystem.SherpaOnnxNemotronStreamingEnRecognitionModel"/>)
    ///     disables the feature entirely: no buffer is allocated and no replay logic runs, so
    ///     behavior and performance are identical to before this feature existed.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="sampleRate"/> is less than or equal to zero, or when
    ///     <paramref name="postEndpointWarmupWindowMs"/> is negative.
    /// </exception>
    /// <remarks>
    ///     Allocates native inference resources. Any failure to load the native library or the
    ///     model files surfaces here as an exception from the sherpa-onnx runtime.
    /// </remarks>
    internal SherpaOnnxRecognitionEngine(OnlineRecognizerConfig config, int sampleRate, int postEndpointWarmupWindowMs = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(postEndpointWarmupWindowMs);

        _sampleRate = sampleRate;
        _recognizer = new OnlineRecognizer(config);
        _stream = _recognizer.CreateStream();

        // The warm-up-replay feature is opt-in and default-disabled: when the configured window
        // is zero, no buffer is allocated and every code path below that checks `_warmupBuffer`
        // for null is skipped, so a model that does not opt in pays no cost whatsoever.
        if (postEndpointWarmupWindowMs > 0)
        {
            _warmupBufferCapacity = (int)((long)sampleRate * postEndpointWarmupWindowMs / 1000);
            _warmupBuffer = new List<float>(_warmupBufferCapacity);
            _postReplayGraceSamples = (int)((long)sampleRate * PostReplayEndpointGraceMs / 1000);
        }
    }

    /// <summary>
    ///     The fixed grace period, in milliseconds of genuinely-new (non-replayed) audio, during
    ///     which a reported endpoint is suppressed immediately after a warm-up replay.
    /// </summary>
    /// <remarks>
    ///     The replayed pre-endpoint buffer is, by construction, mostly the same near-silent
    ///     audio that caused the original endpoint to fire, so feeding it into the freshly reset
    ///     stream can immediately satisfy the trailing-silence endpoint rule again before any
    ///     genuinely new audio has even arrived. This value (1.3s) was empirically validated in
    ///     <c>.agent-logs/planning-nemotron-endpoint-word-loss-warmup-replay-fix-9d4b71.md</c> to
    ///     eliminate that spurious second endpoint in every recording tested, independent of any
    ///     per-model endpoint-rule value - this is bookkeeping internal to the engine, not a
    ///     change to endpoint-detection sensitivity.
    /// </remarks>
    private const int PostReplayEndpointGraceMs = 1300;

    /// <summary>The rate, in Hz, declared for samples supplied to <see cref="AcceptSamples"/>.</summary>
    private readonly int _sampleRate;

    /// <summary>The loaded native streaming recognizer that owns decoding and endpoint detection.</summary>
    private readonly OnlineRecognizer _recognizer;

    /// <summary>
    ///     The stream carrying the audio of the utterance currently being recognized. Not
    ///     <see langword="readonly"/>: the session-end <see cref="Reset"/> replaces it with a
    ///     freshly created stream so that any audio already accepted via
    ///     <c>AcceptWaveform</c> but not yet decoded is discarded along with the abandoned
    ///     stream, rather than surviving into the next session (see <see cref="Reset"/>'s remarks).
    /// </summary>
    private OnlineStream _stream;

    /// <summary>
    ///     The rolling buffer of raw pre-endpoint samples awaiting replay, sized to
    ///     <see cref="_warmupBufferCapacity"/>. <see langword="null"/> when the warm-up-replay
    ///     feature is disabled (the configured window was <c>0</c>), which is also this field's
    ///     sentinel for every "is the feature enabled" check elsewhere in this class.
    /// </summary>
    private readonly List<float>? _warmupBuffer;

    /// <summary>The maximum number of samples <see cref="_warmupBuffer"/> is trimmed to hold.</summary>
    private readonly int _warmupBufferCapacity;

    /// <summary>
    ///     The number of samples of genuinely-new audio that must still be fed before a reported
    ///     endpoint is honored again, counting down from <see cref="_postReplayGraceSamples"/>
    ///     immediately after a replay. Zero when no grace period is in effect.
    /// </summary>
    private int _graceSamplesRemaining;

    /// <summary>The configured grace period (see <see cref="PostReplayEndpointGraceMs"/>), in samples.</summary>
    private readonly int _postReplayGraceSamples;

    /// <summary>
    ///     The text most recently reported as a provisional result, used to suppress duplicate
    ///     events while the recognizer's hypothesis is unchanged.
    /// </summary>
    private string _lastReportedText = string.Empty;

    /// <summary>
    ///     Whether <see cref="OnlineRecognizer.GetResult"/> has produced non-empty text at any
    ///     point since the stream's most recent reset (whether by a prior endpoint or an explicit
    ///     <see cref="Reset"/> call). Always <see langword="false"/> when the warm-up-replay
    ///     feature is disabled (<see cref="_warmupBuffer"/> is <see langword="null"/>), in which
    ///     case it is never consulted.
    /// </summary>
    /// <remarks>
    ///     This is the gate that prevents replaying buffered audio into a freshly reset
    ///     (encoder-cold) stream when the endpoint that triggered the reset fired on leading or
    ///     inter-utterance silence with no genuine speech recognized yet in the current cycle -
    ///     see the "Post-endpoint warm-up replay" design remarks for the corruption this
    ///     otherwise causes (a stray token committed by the cold encoder during the discarded
    ///     replay decode, silently prefixing the next genuinely-spoken utterance).
    ///     <para>
    ///     Deliberately a per-cycle flag, cleared on every reset, rather than a session-lifetime
    ///     "has speech ever occurred" flag: a session-lifetime flag would stay <see
    ///     langword="true"/> forever after the first sentence and would therefore still
    ///     incorrectly permit replay on a later silence-only endpoint occurring after a pause
    ///     between sentences later in the same session. Resetting every cycle requires every
    ///     cycle to independently earn replay eligibility.
    ///     </para>
    ///     <para>
    ///     Deliberately tracked as its own field rather than simply inspecting the current call's
    ///     <c>text</c> at the moment the endpoint fires, as a defense against any case where the
    ///     final endpoint-reporting call's own text could be empty despite genuine speech having
    ///     been decoded earlier in the same cycle (for example if the recognizer's hypothesis
    ///     were to collapse right at the endpoint).
    ///     </para>
    /// </remarks>
    private bool _hasRecognizedTextSinceReset;

    /// <summary>
    ///     Whether the stream's current decoder hypothesis was produced only by
    ///     <see cref="ReplayWarmupBuffer"/> silently pre-warming decoding state, with no
    ///     genuinely-new audio accepted since. Always <see langword="false"/> when the
    ///     warm-up-replay feature is disabled (<see cref="_warmupBuffer"/> is
    ///     <see langword="null"/>), since <see cref="ReplayWarmupBuffer"/> is never called in
    ///     that case.
    /// </summary>
    /// <remarks>
    ///     <see cref="ReplayWarmupBuffer"/> feeds and decodes buffered audio purely to pre-warm
    ///     native decoder state, and deliberately discards the resulting hypothesis text rather
    ///     than ever surfacing it as a <see cref="SpeechRecognitionResult"/>. That hypothesis text
    ///     does not vanish from the stream just because the caller who computed it chose to
    ///     ignore it: it remains whatever <see cref="OnlineRecognizer.GetResult"/> would still
    ///     report until genuinely new audio changes it. Without this flag, both
    ///     <see cref="TryDecode"/> (called again with no intervening <see cref="AcceptSamples"/>,
    ///     for example by a caller draining every result a single captured block produced) and
    ///     <see cref="TryFlush"/> - which also call <c>GetResult</c>, each for a different,
    ///     legitimate reason - would read and report that same still-discarded replay-only
    ///     hypothesis as a result, silently breaking the "replay text is never reported"
    ///     guarantee. Set immediately after a replay, and cleared as soon as
    ///     <see cref="AcceptSamples"/> accepts genuinely new audio into the stream (replay itself
    ///     feeds the native stream directly, bypassing this method, so it never clears its own
    ///     flag).
    /// </remarks>
    private bool _hasReplayOnlyHypothesis;

    /// <summary>Whether <see cref="Dispose"/> has already released the native resources.</summary>
    private bool _isDisposed;

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when the engine has been disposed.</exception>
    public void AcceptSamples(ReadOnlySpan<float> monoSamples)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        // An empty block is a legitimate no-op (for example a capture callback that delivered a
        // partial frame), and passing an empty array through the native boundary buys nothing.
        if (monoSamples.IsEmpty)
        {
            return;
        }

        // The managed sherpa-onnx binding takes a float[] rather than a span, so the block must
        // be materialized as an array before it crosses the interop boundary.
        var samples = monoSamples.ToArray();
        _stream.AcceptWaveform(_sampleRate, samples);

        // Genuinely new audio has now been accepted, so any prior replay-only hypothesis no
        // longer stands alone - see `_hasReplayOnlyHypothesis` remarks. Cleared unconditionally
        // (not gated on `_warmupBuffer`): the flag can only ever be true when the warm-up-replay
        // feature is enabled, so clearing it here when disabled is simply always a no-op.
        _hasReplayOnlyHypothesis = false;

        // Disabled models (`_warmupBuffer` null) skip this entirely: no buffer maintenance, no
        // grace-period bookkeeping, zero measurable behavior change from before this feature.
        if (_warmupBuffer is not null)
        {
            AppendToWarmupBuffer(samples);

            if (_graceSamplesRemaining > 0)
            {
                _graceSamplesRemaining = Math.Max(0, _graceSamplesRemaining - samples.Length);
            }
        }
    }

    /// <summary>
    ///     Appends a block of samples to the rolling warm-up buffer, trimming from the front so
    ///     it never holds more than <see cref="_warmupBufferCapacity"/> samples.
    /// </summary>
    private void AppendToWarmupBuffer(float[] samples)
    {
        var buffer = _warmupBuffer!;
        buffer.AddRange(samples);

        var excess = buffer.Count - _warmupBufferCapacity;
        if (excess > 0)
        {
            buffer.RemoveRange(0, excess);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when the engine has been disposed.</exception>
    public bool TryDecode(out SpeechRecognitionResult? result)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        result = null;

        // The stream's current hypothesis is still the one ReplayWarmupBuffer produced and
        // this engine already discarded; without an intervening AcceptSamples call there is
        // nothing new to decode or report - see `_hasReplayOnlyHypothesis` remarks. This
        // matters because a caller may call this method again for the same captured block with
        // no new audio in between (draining every result one block produced).
        if (_hasReplayOnlyHypothesis)
        {
            return false;
        }

        // Drain every frame the recognizer has enough buffered audio to decode. IsReady goes
        // false once the buffered audio has been consumed, so this loop always terminates.
        while (_recognizer.IsReady(_stream))
        {
            _recognizer.Decode(_stream);
        }

        var text = _recognizer.GetResult(_stream).Text ?? string.Empty;

        if (_warmupBuffer is not null && text.Length > 0)
        {
            _hasRecognizedTextSinceReset = true;
        }

        // An endpoint means the recognizer decided the utterance ended, so whatever text it has
        // is final - except when the warm-up-replay feature is enabled and a post-replay grace
        // period is still counting down, in which case this endpoint is treated as spurious (see
        // the grace-period remarks on `PostReplayEndpointGraceMs`) and falls through to ordinary
        // provisional handling instead of resetting again.
        var isEndpoint = _recognizer.IsEndpoint(_stream);
        if (isEndpoint && _warmupBuffer is not null && _graceSamplesRemaining > 0)
        {
            isEndpoint = false;
        }

        if (isEndpoint)
        {
            // Read replay eligibility BEFORE resetting: only an endpoint that fires after genuine
            // speech has been recognized since the last reset is eligible for warm-up replay. An
            // endpoint firing on leading/inter-utterance silence with nothing genuine recognized
            // yet must not replay - see `_hasRecognizedTextSinceReset` remarks for why.
            var replayEligible = _warmupBuffer is { Count: > 0 } && _hasRecognizedTextSinceReset;

            _recognizer.Reset(_stream);
            _lastReportedText = string.Empty;
            _hasRecognizedTextSinceReset = false;
            _hasReplayOnlyHypothesis = false;

            // Disabled models (`_warmupBuffer` null) skip this entirely - no replay, no grace
            // period, zero measurable behavior change from before this feature existed.
            if (replayEligible)
            {
                ReplayWarmupBuffer();
            }
            else
            {
                _warmupBuffer?.Clear();
            }

            if (text.Length == 0)
            {
                return false;
            }

            result = new SpeechRecognitionResult(text, IsFinal: true);
            return true;
        }

        // Otherwise the text is provisional. Suppress it when there is nothing yet, or when the
        // hypothesis has not moved since the previous call, so a host is not asked to redraw an
        // unchanged transcript for every captured block of silence.
        if (text.Length == 0 || string.Equals(text, _lastReportedText, StringComparison.Ordinal))
        {
            return false;
        }

        _lastReportedText = text;
        result = new SpeechRecognitionResult(text, IsFinal: false);
        return true;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when the engine has been disposed.</exception>
    /// <remarks>
    ///     Calls <see cref="OnlineStream.InputFinished"/>, which tells the native decoder no more
    ///     audio for this stream is coming, so it pads and decodes whatever is still buffered
    ///     instead of holding it back waiting for future context that will now never arrive. This
    ///     is what lets a push-to-talk release with no trailing silence still produce a final
    ///     result for the word it cut off, rather than losing it. Marks the stream finished as a
    ///     side effect, so this is only ever called once, at session end, immediately before
    ///     <see cref="Reset"/> replaces the stream.
    ///     <para>
    ///     Reports nothing if the stream's only hypothesis is one <see cref="ReplayWarmupBuffer"/>
    ///     silently produced and no genuinely-new audio has arrived since - see
    ///     <see cref="_hasReplayOnlyHypothesis"/> - so a caller stopping immediately after a
    ///     replay still never sees the discarded replay text resurface as a flushed result.
    ///     </para>
    /// </remarks>
    public bool TryFlush(out SpeechRecognitionResult? result)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        result = null;

        _stream.InputFinished();

        // Same drain loop as TryDecode: IsReady goes false once the buffered audio (now padded
        // by InputFinished) has been fully consumed, so this always terminates.
        while (_recognizer.IsReady(_stream))
        {
            _recognizer.Decode(_stream);
        }

        if (_hasReplayOnlyHypothesis)
        {
            return false;
        }

        var text = _recognizer.GetResult(_stream).Text ?? string.Empty;
        if (text.Length == 0)
        {
            return false;
        }

        result = new SpeechRecognitionResult(text, IsFinal: true);
        return true;
    }

    /// <summary>
    ///     Silently replays the buffered pre-endpoint audio into the freshly reset stream,
    ///     pre-warming the model's internal decoding state before genuinely new audio arrives, and
    ///     starts the post-replay endpoint grace period.
    /// </summary>
    /// <remarks>
    ///     The replayed audio is fed and decoded exactly as live audio would be, but
    ///     <see cref="OnlineRecognizer.GetResult"/> is called once, purely to keep native decoder
    ///     state consistent, and its text is deliberately discarded - it must never be surfaced as
    ///     a <see cref="SpeechRecognitionResult"/>. Only called when
    ///     <see cref="_warmupBuffer"/> is non-null and non-empty, and only when the endpoint that
    ///     triggered the reset is replay-eligible - i.e. genuine (non-empty) recognized text was
    ///     produced at some point since the stream's most recent reset, per
    ///     <see cref="_hasRecognizedTextSinceReset"/>. Replaying buffered audio into the
    ///     freshly reset (encoder-cold) stream when no genuine speech has occurred yet (a
    ///     leading-silence or inter-utterance-silence-only endpoint) is actively harmful: the
    ///     cold encoder can commit a spurious token while decoding the near-silent replayed
    ///     audio, and because only the replay's <c>GetResult</c> text is discarded - not the
    ///     transducer's persistent autoregressive hypothesis state - that stray token silently
    ///     prefixes whatever text is later reported for the genuinely-spoken utterance that
    ///     follows. This callers-must-check precondition is why every call site computes
    ///     <c>replayEligible</c> before calling this method rather than this method re-deriving
    ///     it.
    /// </remarks>
    private void ReplayWarmupBuffer()
    {
        var buffer = _warmupBuffer!;
        var samples = buffer.ToArray();
        buffer.Clear();

        _stream.AcceptWaveform(_sampleRate, samples);

        while (_recognizer.IsReady(_stream))
        {
            _recognizer.Decode(_stream);
        }

        // Discarded deliberately: this replay exists purely to pre-warm decoding state, and its
        // recognized text must never be raised to a caller. It also remains the stream's current
        // hypothesis until genuinely new audio changes it - see `_hasReplayOnlyHypothesis`
        // remarks for why `TryFlush` must not read it back out later.
        _ = _recognizer.GetResult(_stream);
        _hasReplayOnlyHypothesis = true;

        _graceSamplesRemaining = _postReplayGraceSamples;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when the engine has been disposed.</exception>
    /// <remarks>
    ///     Discards buffered, not-yet-decoded audio - not just the decoder's current hypothesis -
    ///     by creating a replacement stream and disposing the existing one, because
    ///     <see cref="OnlineRecognizer.Reset(OnlineStream)"/> alone only clears the hypothesis:
    ///     a streaming transducer buffers accepted audio it has not yet had enough future context
    ///     to decode, and that buffered audio survives an in-place <c>Reset</c>. Callers call
    ///     <see cref="TryFlush"/> first at session end so that buffered audio is finalized and
    ///     delivered rather than lost here; what this method discards is only whatever a flush
    ///     could not recover. Left undiscarded, it would decode into the next session as soon as
    ///     any audio (even silence) supplied the missing future context, making an abandoned
    ///     utterance bleed into the next <c>Start()</c>. The replacement
    ///     is created - and published to <see cref="_stream"/> and every managed bookkeeping
    ///     field - before the old stream is disposed, so <see cref="_stream"/> always ends up
    ///     holding some stream (never unassigned or disposed) regardless of which side of that
    ///     boundary faults - though not always a usable one, see below: if <c>CreateStream()</c>
    ///     itself throws (native allocation failure), <see cref="_stream"/> is left untouched on
    ///     the old stream rather than an unassigned or disposed one; if instead the old stream's
    ///     own <c>Dispose()</c> throws (native teardown failure) after the replacement was already
    ///     published, the engine is left holding the new, freshly reset stream as intended. The
    ///     two failures are not equally recoverable, because every caller of this method calls
    ///     <see cref="TryFlush"/> on the same stream immediately beforehand: <c>TryFlush</c>
    ///     calls <see cref="OnlineStream.InputFinished"/>, which marks that stream finished, so a
    ///     <c>CreateStream()</c> failure here leaves the engine holding a stream that is not just
    ///     unreset but already finished - unlike a bare <c>Reset()</c> call with no preceding
    ///     flush, it cannot be relied on to accept and decode further audio at all, only to not
    ///     throw doing so. The old-stream-<c>Dispose()</c>-failure case has no such limitation:
    ///     the replacement stream is already current and fully usable. This is deliberately
    ///     distinct from the endpoint-triggered reset inside <see cref="TryDecode"/>, which resets
    ///     the same stream in place because that path is a normal utterance boundary where the
    ///     buffered pre/post-endpoint audio is wanted for <see cref="ReplayWarmupBuffer"/>.
    /// </remarks>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var previousStream = _stream;
        _stream = _recognizer.CreateStream();

        _lastReportedText = string.Empty;
        _hasRecognizedTextSinceReset = false;
        _hasReplayOnlyHypothesis = false;
        _warmupBuffer?.Clear();
        _graceSamplesRemaining = 0;

        previousStream.Dispose();
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Releases the native stream and recognizer, in that order, because the stream is owned
    ///     by the recognizer that created it. Disposes whichever stream is current at the time of
    ///     disposal - <see cref="_stream"/> may have been replaced since construction by a prior
    ///     session-end <see cref="Reset"/> call. Safe to call more than once.
    /// </remarks>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _stream.Dispose();
        _recognizer.Dispose();
    }
}
