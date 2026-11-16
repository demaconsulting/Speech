namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     What an <see cref="ITextAnalyzer"/> found in a text: opaque annotations plus, optionally,
///     authoritative sentence boundaries.
/// </summary>
/// <param name="Annotations">
///     The annotations, with offsets relative to the analyzed text, in the order the analyzer
///     supplied them. Overlapping annotations are allowed (different kinds may cover the same
///     word). Must not be null and must not contain null.
/// </param>
/// <param name="SentenceBreaks">
///     <see langword="null"/> when the analyzer expresses no sentence opinion and the punctuation
///     heuristic of <see cref="SentenceChunker"/> should be used; otherwise the authoritative
///     sentence boundaries, even when the list is empty (meaning "no sentence splits"). A value
///     <c>k</c> means a sentence ends immediately before index <c>k</c> of the analyzed text
///     (so <c>k</c> is the start of the next sentence); <c>0</c> and the text length are
///     harmless no-ops. Entries must not be negative.
/// </param>
/// <remarks>
///     The result is stored defensively: the annotation list is copied, and the break list is
///     stored as a sorted, de-duplicated private copy. Equality compares the list members by
///     reference, like the other list-carrying records in this library. The record is immutable
///     and therefore safe to share across threads.
/// </remarks>
/// <exception cref="ArgumentNullException"><paramref name="Annotations"/> is null.</exception>
/// <exception cref="ArgumentException"><paramref name="Annotations"/> contains a null element.</exception>
/// <exception cref="ArgumentOutOfRangeException"><paramref name="SentenceBreaks"/> contains a negative entry.</exception>
public sealed record TextAnalysisResult(IReadOnlyList<SegmentAnnotation> Annotations, IReadOnlyList<int>? SentenceBreaks)
{
    /// <summary>Backing field for <see cref="Annotations"/>.</summary>
    private readonly IReadOnlyList<SegmentAnnotation> _annotations = AnnotationList.Copy(Annotations, nameof(Annotations));

    /// <summary>Backing field for <see cref="SentenceBreaks"/>.</summary>
    private readonly IReadOnlyList<int>? _sentenceBreaks = CopyBreaks(SentenceBreaks);

    /// <summary>
    ///     Gets the shared result that carries no annotations and no sentence opinion.
    /// </summary>
    public static TextAnalysisResult Empty { get; } = new(AnnotationList.Empty, null);

    /// <summary>
    ///     Gets the annotations, relative to the analyzed text; never null.
    /// </summary>
    public IReadOnlyList<SegmentAnnotation> Annotations
    {
        get => _annotations;
        init => _annotations = AnnotationList.Copy(value, nameof(Annotations));
    }

    /// <summary>
    ///     Gets the authoritative sentence boundaries as a sorted, de-duplicated list, or
    ///     <see langword="null"/> when the analyzer expresses no sentence opinion.
    /// </summary>
    public IReadOnlyList<int>? SentenceBreaks
    {
        get => _sentenceBreaks;
        init => _sentenceBreaks = CopyBreaks(value);
    }

    /// <summary>
    ///     Validates, sorts, de-duplicates and copies a break list.
    /// </summary>
    /// <param name="value">The candidate break list, or null.</param>
    /// <returns>Null when <paramref name="value"/> is null; otherwise an immutable sorted, distinct copy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> contains a negative entry.</exception>
    private static IReadOnlyList<int>? CopyBreaks(IReadOnlyList<int>? value)
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
                throw new ArgumentOutOfRangeException(nameof(SentenceBreaks), value[i], "Sentence breaks must not be negative.");
            }

            copy[i] = value[i];
        }

        Array.Sort(copy);
        return Array.AsReadOnly(copy.Distinct().ToArray());
    }
}
