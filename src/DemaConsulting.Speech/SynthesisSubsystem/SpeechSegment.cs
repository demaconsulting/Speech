namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     One ordered element of a <see cref="SpeechPlan"/>: a single synthesis call's worth of text
///     (possibly empty, for a pure-pause segment) plus any real inserted silence and per-model
///     parameter overrides that apply only while this segment is spoken.
/// </summary>
/// <param name="Text">
///     The text to synthesize for this segment. May be empty, in which case this segment carries
///     only silence (<paramref name="PreSilenceMs"/>/<paramref name="PostSilenceMs"/>) and no
///     synthesis call is made for it.
/// </param>
/// <param name="PreSilenceMs">
///     Real silence, in milliseconds, to insert immediately before this segment's audio. Must not
///     be negative.
/// </param>
/// <param name="PostSilenceMs">
///     Real silence, in milliseconds, to insert immediately after this segment's audio. Must not
///     be negative.
/// </param>
/// <param name="ParameterOverrides">
///     A model-declared parameter id to overridden value bag that applies only to this segment
///     (for example a <c>ParameterMapped</c> tag raising a declared tempo parameter), or
///     <see langword="null"/> when no override applies. Never contains a key the model does not
///     declare in <see cref="DemaConsulting.Speech.ModelManagementSubsystem.ISpeechModel.Parameters"/>.
/// </param>
/// <remarks>
///     Per this library's "two-layer tag rendering" decision, pauses always render as real
///     inserted silence regardless of model capability, so
///     <see cref="NaturalLanguageAudioTag.ShortPause"/>/<see cref="NaturalLanguageAudioTag.LongPause"/>
///     always produce silence here rather than a parameter override or passthrough text. This
///     record is immutable and safe to share across threads. Public, mirroring
///     <see cref="SpeechPlan"/>'s public visibility.
/// </remarks>
/// <exception cref="ArgumentNullException"><paramref name="Text"/> is null.</exception>
/// <exception cref="ArgumentOutOfRangeException">
///     <paramref name="PreSilenceMs"/> or <paramref name="PostSilenceMs"/> is negative.
/// </exception>
public sealed record SpeechSegment(
    string Text,
    int PreSilenceMs,
    int PostSilenceMs,
    IReadOnlyDictionary<string, object>? ParameterOverrides)
{
    private readonly string _text = RequireText(Text);
    private readonly int _preSilenceMs = RequireNonNegative(PreSilenceMs, nameof(PreSilenceMs));
    private readonly int _postSilenceMs = RequireNonNegative(PostSilenceMs, nameof(PostSilenceMs));

    /// <summary>
    ///     Gets the text to synthesize for this segment; never null, may be empty.
    /// </summary>
    public string Text
    {
        get => _text;
        init => _text = RequireText(value);
    }

    /// <summary>
    ///     Gets the real silence, in milliseconds, inserted immediately before this segment's audio.
    /// </summary>
    public int PreSilenceMs
    {
        get => _preSilenceMs;
        init => _preSilenceMs = RequireNonNegative(value, nameof(PreSilenceMs));
    }

    /// <summary>
    ///     Gets the real silence, in milliseconds, inserted immediately after this segment's audio.
    /// </summary>
    public int PostSilenceMs
    {
        get => _postSilenceMs;
        init => _postSilenceMs = RequireNonNegative(value, nameof(PostSilenceMs));
    }

    /// <summary>
    ///     Rejects a null text.
    /// </summary>
    /// <param name="text">The candidate text.</param>
    /// <returns>The validated text.</returns>
    private static string RequireText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text;
    }

    /// <summary>
    ///     Rejects a negative silence duration.
    /// </summary>
    /// <param name="value">The candidate duration in milliseconds.</param>
    /// <param name="paramName">The name of the offending member.</param>
    /// <returns>The validated duration.</returns>
    private static int RequireNonNegative(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Silence must not be negative.");
        }

        return value;
    }
}
