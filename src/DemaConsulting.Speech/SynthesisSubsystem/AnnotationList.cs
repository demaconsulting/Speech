namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Internal helpers shared by every type that carries an immutable list of
///     <see cref="SegmentAnnotation"/>s: a shared empty singleton, a defensive validating copy
///     and a bounds check against the text the annotations refer to.
/// </summary>
/// <remarks>
///     A single shared <see cref="Empty"/> instance lets default-constructed spans and segments
///     stay record-equal to instances created before annotations existed. All members are
///     stateless and thread-safe.
/// </remarks>
internal static class AnnotationList
{
    /// <summary>The shared, immutable, empty annotation list.</summary>
    internal static readonly IReadOnlyList<SegmentAnnotation> Empty = Array.Empty<SegmentAnnotation>();

    /// <summary>
    ///     Validates and defensively copies a caller-supplied annotation list so later changes to
    ///     the caller's list cannot affect the owning record.
    /// </summary>
    /// <param name="value">The list to copy. Must not be null and must not contain null.</param>
    /// <param name="paramName">The name of the offending member, for exceptions.</param>
    /// <returns>
    ///     <see cref="Empty"/> when the list is empty; otherwise an immutable private copy that
    ///     preserves order.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> contains a null element.</exception>
    internal static IReadOnlyList<SegmentAnnotation> Copy(IReadOnlyList<SegmentAnnotation>? value, string paramName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(paramName);
        }

        if (value.Count == 0)
        {
            return Empty;
        }

        var copy = new SegmentAnnotation[value.Count];
        for (var i = 0; i < copy.Length; i++)
        {
            copy[i] = value[i] ?? throw new ArgumentException("Annotations must not contain null.", paramName);
        }

        return Array.AsReadOnly(copy);
    }

    /// <summary>
    ///     Rejects any annotation whose range extends past the end of the text it refers to.
    /// </summary>
    /// <param name="list">The annotations to check.</param>
    /// <param name="textLength">The length of the text the annotations refer to.</param>
    /// <param name="paramName">The name of the offending member, for exceptions.</param>
    /// <exception cref="ArgumentException">An annotation lies outside the text.</exception>
    internal static void RequireWithin(IReadOnlyList<SegmentAnnotation> list, int textLength, string paramName)
    {
        foreach (var annotation in list)
        {
            if (annotation.End > textLength)
            {
                throw new ArgumentException("Annotation lies outside the text.", paramName);
            }
        }
    }
}
