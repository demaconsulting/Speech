using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

/// <summary>
///     The reading and pronunciation annotations of a segment, indexed by the original span each one covers.
/// </summary>
/// <remarks>
///     Both annotation kinds are anchored to one word token of the original text and never overlap a replacement.
///     A malformed annotation (an invalid reading id, an unknown scheme) is simply not indexed, as the typed readers of
///     the text analysis package decide; the phonemizer then speaks the word as usual. At most one annotation per
///     span and kind is kept (the first). Immutable once built.
/// </remarks>
internal sealed class AnnotationIndex
{
    /// <summary>An index with no annotations.</summary>
    public static readonly AnnotationIndex Empty = new([], []);

    /// <summary>The reading ids by span.</summary>
    private readonly Dictionary<(int Start, int Length), string> _readings;

    /// <summary>The pronunciation overrides by span.</summary>
    private readonly Dictionary<(int Start, int Length), (PronunciationScheme Scheme, string Phonemes)> _pronunciations;

    /// <summary>
    ///     Initializes the index.
    /// </summary>
    /// <param name="readings">The reading ids by span.</param>
    /// <param name="pronunciations">The pronunciation overrides by span.</param>
    private AnnotationIndex(
        Dictionary<(int Start, int Length), string> readings,
        Dictionary<(int Start, int Length), (PronunciationScheme Scheme, string Phonemes)> pronunciations)
    {
        _readings = readings;
        _pronunciations = pronunciations;
    }

    /// <summary>
    ///     Indexes the reading and pronunciation annotations.
    /// </summary>
    /// <param name="annotations">The annotations, or <see langword="null"/>.</param>
    /// <returns>The index.</returns>
    public static AnnotationIndex Build(IReadOnlyList<SegmentAnnotation>? annotations)
    {
        if (annotations is null || annotations.Count == 0)
        {
            return Empty;
        }

        var readings = new Dictionary<(int Start, int Length), string>();
        var pronunciations = new Dictionary<(int Start, int Length), (PronunciationScheme Scheme, string Phonemes)>();
        foreach (var annotation in annotations)
        {
            var key = (annotation.Start, annotation.Length);
            if (TextEnAnnotations.TryReadReading(annotation, out var readingId))
            {
                readings.TryAdd(key, readingId);
            }
            else if (TextEnAnnotations.TryReadPronunciation(annotation, out var scheme, out var phonemes))
            {
                pronunciations.TryAdd(key, (scheme, phonemes));
            }
        }

        return new AnnotationIndex(readings, pronunciations);
    }

    /// <summary>
    ///     Finds the reading annotation of a token.
    /// </summary>
    /// <param name="start">The token's start in the original text.</param>
    /// <param name="length">The length of the original span of the token.</param>
    /// <param name="partialLimit">The longest annotated span shorter than <paramref name="length"/> that may be taken (the unchanged headword of the token).</param>
    /// <param name="readingId">Receives the reading id.</param>
    /// <param name="coveredLength">
    ///     Receives the length of the annotated span: <paramref name="length"/> for an exact match, shorter when the
    ///     annotation covers only the start of the token (the text analysis tokenizes <c>record's</c> as <c>record</c> and
    ///     <c>'s</c>).
    /// </param>
    /// <returns><see langword="true"/> when a reading annotation starts at the token and does not extend beyond it.</returns>
    public bool TryGetReading(int start, int length, int partialLimit, out string readingId, out int coveredLength) =>
        TryGet(_readings, start, length, partialLimit, out readingId, out coveredLength);

    /// <summary>
    ///     Finds the pronunciation override of a token.
    /// </summary>
    /// <param name="start">The token's start in the original text.</param>
    /// <param name="length">The length of the original span of the token.</param>
    /// <param name="partialLimit">The longest annotated span shorter than <paramref name="length"/> that may be taken (the unchanged headword of the token).</param>
    /// <param name="scheme">Receives the scheme.</param>
    /// <param name="phonemes">Receives the phonemes.</param>
    /// <param name="coveredLength">
    ///     Receives the length of the annotated span: <paramref name="length"/> for an exact match, shorter when the
    ///     override covers only the start of the token (the headword of a possessive or contraction).
    /// </param>
    /// <returns><see langword="true"/> when an override starts at the token and does not extend beyond it.</returns>
    public bool TryGetPronunciation(int start, int length, int partialLimit, out PronunciationScheme scheme, out string phonemes, out int coveredLength)
    {
        var found = TryGet(_pronunciations, start, length, partialLimit, out var value, out coveredLength);
        scheme = value.Scheme;
        phonemes = value.Phonemes ?? string.Empty;
        return found;
    }

    /// <summary>
    ///     Finds the annotation of a token: the exact span first, then the longest shorter span up to a limit.
    /// </summary>
    /// <typeparam name="T">The annotation value type.</typeparam>
    /// <param name="annotations">The annotations of one kind by span.</param>
    /// <param name="start">The token's start.</param>
    /// <param name="length">The length of the token's span.</param>
    /// <param name="partialLimit">The longest shorter span that may be taken.</param>
    /// <param name="value">Receives the annotation value.</param>
    /// <param name="coveredLength">Receives the length of the span found.</param>
    /// <returns><see langword="true"/> when an annotation was found.</returns>
    private static bool TryGet<T>(Dictionary<(int Start, int Length), T> annotations, int start, int length, int partialLimit, out T value, out int coveredLength)
    {
        if (annotations.TryGetValue((start, length), out value!))
        {
            coveredLength = length;
            return true;
        }

        // The annotated word may be only the start of the token (a possessive or contraction follows it)
        for (var shorter = Math.Min(length - 1, partialLimit); shorter > 0; shorter--)
        {
            if (annotations.TryGetValue((start, shorter), out value!))
            {
                coveredLength = shorter;
                return true;
            }
        }

        coveredLength = 0;
        return false;
    }
}
