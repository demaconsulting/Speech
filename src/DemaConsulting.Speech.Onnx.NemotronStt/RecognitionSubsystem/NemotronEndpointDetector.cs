namespace DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

/// <summary>
///     The endpoint rules of <see cref="NemotronEndpointDetector"/>: how much trailing silence or
///     utterance length ends an utterance. The defaults equal sherpa-onnx's online recognizer
///     defaults, so the ONNX Nemotron backend segments speech like the Sherpa Nemotron backend.
/// </summary>
/// <param name="TrailingSilenceMs">
///     Rule 1: the trailing silence, in milliseconds, that ends a segment whether or not it holds
///     speech (default 2400). A segment without speech produces no text, so this rule only bounds
///     the silence clock during long pauses. Zero disables the rule.
/// </param>
/// <param name="TrailingSilenceAfterSpeechMs">
///     Rule 2: the trailing silence, in milliseconds, after the last decoded token that ends an
///     utterance holding speech (default 1200). This is the rule that finalizes ordinary
///     utterances. Zero disables the rule.
/// </param>
/// <param name="MaxUtteranceMs">
///     Rule 3: the utterance length, in milliseconds, at which an utterance is ended even while
///     speech continues (default 20000), so a long monologue still yields finals. Zero disables
///     the rule.
/// </param>
internal sealed record NemotronEndpointRules(
    int TrailingSilenceMs = 2400,
    int TrailingSilenceAfterSpeechMs = 1200,
    int MaxUtteranceMs = 20000);

/// <summary>
///     The rule that ended an utterance, as reported by <see cref="NemotronEndpointDetector.Check"/>.
/// </summary>
internal enum NemotronEndpointReason
{
    /// <summary>No rule is satisfied; the utterance continues.</summary>
    None,

    /// <summary>Rule 1: the trailing silence reached <see cref="NemotronEndpointRules.TrailingSilenceMs"/>.</summary>
    TrailingSilence,

    /// <summary>Rule 2: after speech, the trailing silence reached <see cref="NemotronEndpointRules.TrailingSilenceAfterSpeechMs"/>.</summary>
    TrailingSilenceAfterSpeech,

    /// <summary>Rule 3: the utterance length reached <see cref="NemotronEndpointRules.MaxUtteranceMs"/>.</summary>
    MaxUtteranceLength,
}

/// <summary>
///     Decides when an utterance ends, from what the decoder produced rather than from the input
///     loudness: Sherpa-equivalent trailing-silence and maximum-length rules on the decode clock.
/// </summary>
/// <remarks>
///     <b>Why the decode clock</b>: "silence" for endpointing means "the recognizer emitted no
///     token", not "the input was quiet". A steady background noise that never reads as quiet
///     still yields blank frames, and a quiet word still yields a token, so counting decoded blank
///     frames ends utterances where the recognizer heard the speech end - the rule sherpa-onnx's
///     online recognizer applies. The recognition engine reports each decoded encoder frame with
///     <see cref="AdvanceFrame"/>.
///     <para>
///     <b>Silence the decoder never sees</b>: the engine's <see cref="SilenceRunLimiter"/> removes
///     most of a long quiet run before feature extraction, so those samples never become encoder
///     frames. The engine reports them with <see cref="AdvanceSilence"/> at the point in the
///     stream where they were removed; they count as trailing silence and utterance length exactly
///     as decoded blank frames would, so a pause of a given input duration ends an utterance after
///     the same input time whether or not the limiter shortened it.
///     </para>
///     <para>
///     <b>Padding never advances the clocks</b>: synthetic silence the engine inserts to complete
///     an encoder chunk is reported with <see cref="AdvancePaddingFrame"/>. A token decoded on it
///     still counts as speech, but its duration counts toward neither clock, so padding alone can
///     never satisfy a rule - otherwise every flush or quiet-run pad would shift the endpoints.
///     </para>
///     <para>
///     <b>State rules</b>: a token marks the segment as holding speech and restarts the trailing
///     silence; a blank real frame lengthens the trailing silence; real frames and removed silence
///     lengthen the utterance. <see cref="Check"/> evaluates rule 2, then rule 1, then rule 3 and
///     is pure; <see cref="Reset()"/> starts the next segment. The detector never touches audio or
///     ONNX Runtime and is fully deterministic. Not thread-safe; owned by one engine.
///     </para>
/// </remarks>
internal sealed class NemotronEndpointDetector
{
    /// <summary>The rule-1 threshold in samples, or zero when the rule is disabled.</summary>
    private readonly long _trailingSilenceSamples;

    /// <summary>The rule-2 threshold in samples, or zero when the rule is disabled.</summary>
    private readonly long _trailingSilenceAfterSpeechSamples;

    /// <summary>The rule-3 threshold in samples, or zero when the rule is disabled.</summary>
    private readonly long _maxUtteranceSamples;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NemotronEndpointDetector"/> class.
    /// </summary>
    /// <param name="rules">The rules, or <see langword="null"/> for the Sherpa-equivalent defaults.</param>
    /// <param name="sampleRate">The input sample rate in hertz used to convert the rule durations; must be positive (default 16000).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when a rule duration is negative or <paramref name="sampleRate"/> is not positive.
    /// </exception>
    public NemotronEndpointDetector(NemotronEndpointRules? rules = null, int sampleRate = NemotronFeatureExtractor.SampleRate)
    {
        // Reject invalid configuration up front: a negative duration has no meaning, and a zero
        // or negative rate cannot convert durations to samples
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        var effective = rules ?? new NemotronEndpointRules();
        if (effective.TrailingSilenceMs < 0 ||
            effective.TrailingSilenceAfterSpeechMs < 0 ||
            effective.MaxUtteranceMs < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rules),
                effective,
                "Endpoint rule durations must not be negative (zero disables a rule).");
        }

        // Convert each rule to samples once so every frame update is integer arithmetic
        Rules = effective;
        _trailingSilenceSamples = ToSamples(effective.TrailingSilenceMs, sampleRate);
        _trailingSilenceAfterSpeechSamples = ToSamples(effective.TrailingSilenceAfterSpeechMs, sampleRate);
        _maxUtteranceSamples = ToSamples(effective.MaxUtteranceMs, sampleRate);
    }

    /// <summary>Gets the rules in effect.</summary>
    public NemotronEndpointRules Rules { get; }

    /// <summary>Gets a value indicating whether the current segment holds speech (any decoded token).</summary>
    public bool HasSpeech { get; private set; }

    /// <summary>Gets the silence, in input samples, since the last token (or the segment start).</summary>
    public long TrailingSilenceSamples { get; private set; }

    /// <summary>Gets the length, in input samples, of the current segment (padding excluded).</summary>
    public long UtteranceSamples { get; private set; }

    /// <summary>
    ///     Records one decoded encoder frame of real audio.
    /// </summary>
    /// <param name="frameSamples">The input duration the frame represents, in samples; must not be negative.</param>
    /// <param name="emittedToken">Whether the decoder emitted at least one token on the frame.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="frameSamples"/> is negative.</exception>
    public void AdvanceFrame(long frameSamples, bool emittedToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameSamples);

        // The frame is part of the utterance either way; only a blank frame is trailing silence
        UtteranceSamples += frameSamples;
        if (emittedToken)
        {
            MarkSpeech();
        }
        else
        {
            TrailingSilenceSamples += frameSamples;
        }
    }

    /// <summary>
    ///     Records one decoded encoder frame of synthetic padding: a token still counts as speech,
    ///     but neither clock advances.
    /// </summary>
    /// <param name="emittedToken">Whether the decoder emitted at least one token on the frame.</param>
    public void AdvancePaddingFrame(bool emittedToken)
    {
        if (emittedToken)
        {
            MarkSpeech();
        }
    }

    /// <summary>
    ///     Records quiet input the silence-run limiter removed before feature extraction; it
    ///     counts as trailing silence and utterance length.
    /// </summary>
    /// <param name="droppedSamples">The number of removed input samples; must not be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="droppedSamples"/> is negative.</exception>
    public void AdvanceSilence(long droppedSamples)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(droppedSamples);

        TrailingSilenceSamples += droppedSamples;
        UtteranceSamples += droppedSamples;
    }

    /// <summary>
    ///     Evaluates the rules against the current segment without changing any state.
    /// </summary>
    /// <returns>
    ///     The first satisfied rule in the order rule 2 (silence after speech), rule 1 (any
    ///     trailing silence), rule 3 (maximum length); <see cref="NemotronEndpointReason.None"/>
    ///     when none is satisfied. A disabled rule is never satisfied.
    /// </returns>
    public NemotronEndpointReason Check()
    {
        if (HasSpeech && IsReached(TrailingSilenceSamples, _trailingSilenceAfterSpeechSamples))
        {
            return NemotronEndpointReason.TrailingSilenceAfterSpeech;
        }

        if (IsReached(TrailingSilenceSamples, _trailingSilenceSamples))
        {
            return NemotronEndpointReason.TrailingSilence;
        }

        return IsReached(UtteranceSamples, _maxUtteranceSamples)
            ? NemotronEndpointReason.MaxUtteranceLength
            : NemotronEndpointReason.None;
    }

    /// <summary>
    ///     Starts a new, empty segment: no speech, both clocks at zero.
    /// </summary>
    public void Reset() => Reset(carriesSpeech: false);

    /// <summary>
    ///     Starts a new segment, optionally one that already holds speech.
    /// </summary>
    /// <param name="carriesSpeech">
    ///     <see langword="true"/> when the engine carried decoded tokens into the new segment (a
    ///     word cut by the maximum-length rule), so rule 2 applies to it from the start.
    /// </param>
    public void Reset(bool carriesSpeech)
    {
        HasSpeech = carriesSpeech;
        TrailingSilenceSamples = 0;
        UtteranceSamples = 0;
    }

    /// <summary>Converts a rule duration in milliseconds to samples.</summary>
    /// <param name="milliseconds">The non-negative duration.</param>
    /// <param name="sampleRate">The positive sample rate.</param>
    /// <returns>The duration in samples; zero keeps the rule disabled.</returns>
    private static long ToSamples(int milliseconds, int sampleRate) => (long)milliseconds * sampleRate / 1000;

    /// <summary>Tests a clock against a rule threshold; a zero threshold is a disabled rule.</summary>
    /// <param name="value">The clock value in samples.</param>
    /// <param name="threshold">The rule threshold in samples, or zero when disabled.</param>
    /// <returns><see langword="true"/> when the rule is enabled and the clock reached it.</returns>
    private static bool IsReached(long value, long threshold) => threshold > 0 && value >= threshold;

    /// <summary>Records a decoded token: the segment holds speech and the trailing silence restarts.</summary>
    private void MarkSpeech()
    {
        HasSpeech = true;
        TrailingSilenceSamples = 0;
    }
}
