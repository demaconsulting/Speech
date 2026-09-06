namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     One ordered element of <see cref="AudioTagParser.Parse"/>'s neutral intermediate
///     representation: either a run of literal text or a single recognized Natural Language
///     Audio Tag.
/// </summary>
/// <param name="Kind">Which shape this span carries.</param>
/// <param name="Text">
///     Populated when <paramref name="Kind"/> is <see cref="TaggedTextSpanKind.PlainText"/>: the
///     literal narration text, which includes any unrecognized or malformed bracket content
///     (unknown tag names, unclosed brackets, empty brackets) exactly as it appeared in the
///     input. Empty when <paramref name="Kind"/> is <see cref="TaggedTextSpanKind.Tag"/>.
/// </param>
/// <param name="Tag">
///     Populated only when <paramref name="Kind"/> is <see cref="TaggedTextSpanKind.Tag"/>;
///     <see langword="null"/> for <see cref="TaggedTextSpanKind.PlainText"/>.
/// </param>
/// <remarks>
///     This type is a pure, model-independent value shape: it carries no opinion about how a
///     tag should be rendered into audio. Layer 2 per-model rendering (a later phase) consumes an
///     ordered sequence of these spans to build a <c>SpeechPlan</c>. The record is immutable and
///     therefore safe to share across threads.
/// </remarks>
public sealed record TaggedTextSpan(TaggedTextSpanKind Kind, string Text, NaturalLanguageAudioTag? Tag)
{
    /// <summary>
    ///     Creates a <see cref="TaggedTextSpanKind.PlainText"/> span.
    /// </summary>
    /// <param name="text">The literal text this span carries. Must not be <see langword="null"/>.</param>
    /// <returns>A new plain-text span.</returns>
    public static TaggedTextSpan PlainText(string text) => new(TaggedTextSpanKind.PlainText, text, null);

    /// <summary>
    ///     Creates a <see cref="TaggedTextSpanKind.Tag"/> span.
    /// </summary>
    /// <param name="tag">The recognized tag this span carries.</param>
    /// <returns>A new tag span.</returns>
    public static TaggedTextSpan TagSpan(NaturalLanguageAudioTag tag) => new(TaggedTextSpanKind.Tag, string.Empty, tag);
}
