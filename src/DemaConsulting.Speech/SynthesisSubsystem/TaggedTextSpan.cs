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
///     tag should be rendered into audio. Layer 2 per-model rendering consumes an
///     ordered sequence of these spans to build a <c>SpeechPlan</c>. A plain-text span may
///     additionally carry <see cref="Annotations"/> and <see cref="SentenceBreaks"/> from a text
///     analysis (see <see cref="ITextAnalyzer"/>), and a tag span may carry a
///     <see cref="SentenceBreaks"/> entry for an analyzer break adjacent to it; both default to
///     "none" so a span created
///     without them is equal to one created before they existed. <see cref="Annotations"/>
///     compares by reference, and the shared empty list keeps default spans equal. The record
///     is immutable and therefore safe to share across threads.
/// </remarks>
public sealed record TaggedTextSpan(TaggedTextSpanKind Kind, string Text, NaturalLanguageAudioTag? Tag)
{
    /// <summary>Backing field for <see cref="Annotations"/>.</summary>
    private readonly IReadOnlyList<SegmentAnnotation> _annotations = AnnotationList.Empty;

    /// <summary>Backing field for <see cref="SentenceBreaks"/>.</summary>
    private readonly IReadOnlyList<int>? _sentenceBreaks;

    /// <summary>
    ///     Gets the annotations for this span, with offsets relative to <see cref="Text"/>; empty
    ///     by default. Only meaningful for <see cref="TaggedTextSpanKind.PlainText"/> spans. The
    ///     bounds against <see cref="Text"/> are not validated here; consumers ignore
    ///     out-of-range entries defensively.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
    /// <exception cref="ArgumentException">Thrown when the list contains null.</exception>
    public IReadOnlyList<SegmentAnnotation> Annotations
    {
        get => _annotations;
        init => _annotations = AnnotationList.Copy(value, nameof(Annotations));
    }

    /// <summary>
    ///     Gets the sentence breaks for this span, or <see langword="null"/> (the default) when no
    ///     sentence opinion was supplied. For a <see cref="TaggedTextSpanKind.PlainText"/> span the
    ///     offsets are relative to <see cref="Text"/>, and a non-null list, even an empty one, is
    ///     authoritative for the whole text being chunked. For a <see cref="TaggedTextSpanKind.Tag"/>
    ///     span the offsets are relative to the tag's single position in the text: <c>0</c> is
    ///     immediately before the tag and <c>1</c> immediately after it (other values are
    ///     ignored); this carries an analyzer's sentence break that falls between consecutive
    ///     tags, where no plain text exists to hold it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the list contains a negative entry.</exception>
    public IReadOnlyList<int>? SentenceBreaks
    {
        get => _sentenceBreaks;
        init => _sentenceBreaks = CopyBreaks(value, nameof(SentenceBreaks));
    }

    /// <summary>
    ///     Validates and copies a sentence-break list, keeping its order.
    /// </summary>
    /// <param name="value">The candidate list, or null.</param>
    /// <param name="paramName">The name of the offending member, for exceptions.</param>
    /// <returns>Null when <paramref name="value"/> is null; otherwise an immutable copy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> contains a negative entry.</exception>
    private static IReadOnlyList<int>? CopyBreaks(IReadOnlyList<int>? value, string paramName)
    {
        if (value is null)
        {
            return null;
        }

        var copy = new int[value.Count];
        for (var i = 0; i < copy.Length; i++)
        {
            if (value[i] < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value[i], "Sentence breaks must not be negative.");
            }

            copy[i] = value[i];
        }

        return Array.AsReadOnly(copy);
    }

    /// <summary>
    ///     Creates a <see cref="TaggedTextSpanKind.PlainText"/> span.
    /// </summary>
    /// <param name="text">The literal text this span carries. Must not be <see langword="null"/>.</param>
    /// <returns>A new plain-text span.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public static TaggedTextSpan PlainText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new(TaggedTextSpanKind.PlainText, text, null);
    }

    /// <summary>
    ///     Creates a <see cref="TaggedTextSpanKind.PlainText"/> span that carries text-analysis
    ///     results.
    /// </summary>
    /// <param name="text">The literal text this span carries. Must not be <see langword="null"/>.</param>
    /// <param name="annotations">The annotations, relative to <paramref name="text"/>. Must not be null.</param>
    /// <param name="sentenceBreaks">
    ///     The sentence breaks, relative to <paramref name="text"/>, or <see langword="null"/> for
    ///     no sentence opinion.
    /// </param>
    /// <returns>A new plain-text span.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="text"/> or <paramref name="annotations"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="annotations"/> contains a null element.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sentenceBreaks"/> contains a negative entry.</exception>
    public static TaggedTextSpan PlainText(
        string text,
        IReadOnlyList<SegmentAnnotation> annotations,
        IReadOnlyList<int>? sentenceBreaks)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new TaggedTextSpan(TaggedTextSpanKind.PlainText, text, null)
        {
            Annotations = annotations,
            SentenceBreaks = sentenceBreaks,
        };
    }

    /// <summary>
    ///     Creates a <see cref="TaggedTextSpanKind.Tag"/> span.
    /// </summary>
    /// <param name="tag">The recognized tag this span carries.</param>
    /// <returns>A new tag span.</returns>
    public static TaggedTextSpan TagSpan(NaturalLanguageAudioTag tag) => new(TaggedTextSpanKind.Tag, string.Empty, tag);
}
