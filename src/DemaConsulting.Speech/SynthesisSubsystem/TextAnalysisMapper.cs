using System.Text;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Internal mapper between a parsed <see cref="TaggedTextSpan"/> sequence and the single
///     analysis text an <see cref="ITextAnalyzer"/> sees: builds that text with one
///     <see cref="TextAnalysisConventions.TagPlaceholder"/> per tag, and re-bases the analyzer's
///     annotations and sentence breaks back onto the plain-text spans.
/// </summary>
/// <remarks>
///     Offsets in the analysis text <c>A</c> are interval-mapped: each plain span <c>i</c>
///     occupies <c>[a_i, b_i)</c> in <c>A</c>, and every tag occupies exactly one placeholder
///     character between spans. Because every tag contributes at least one character, two plain
///     spans never touch, so a closed-interval lookup <c>a_i &lt;= k &lt;= b_i</c> is
///     unambiguous. An annotation that cannot sit wholly inside one plain span (it covers a
///     placeholder, crosses two spans, or lies outside the text) is dropped, never clipped or
///     split, because a clipped annotation would misrepresent the annotated unit. A sentence
///     break that touches a plain span goes to that span; a break strictly inside a run of tags
///     attaches to the tag span just before it (tag offset <c>1</c>), and one before a leading
///     tag or beyond the text is dropped. The type is
///     stateless and thread-safe.
/// </remarks>
internal static class TextAnalysisMapper
{
    /// <summary>
    ///     Builds the text to analyze: every plain span's text in order, with exactly one
    ///     <see cref="TextAnalysisConventions.TagPlaceholder"/> in place of each tag span.
    /// </summary>
    /// <param name="spans">The parsed spans. Must not be null.</param>
    /// <returns>The analysis text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spans"/> is null.</exception>
    internal static string BuildAnalysisText(IReadOnlyList<TaggedTextSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);

        var builder = new StringBuilder();
        foreach (var span in spans)
        {
            if (span.Kind == TaggedTextSpanKind.PlainText)
            {
                builder.Append(span.Text);
            }
            else
            {
                builder.Append(TextAnalysisConventions.TagPlaceholder);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Attaches an analysis result to the plain-text spans it belongs to, re-basing every
    ///     offset to be relative to its own span.
    /// </summary>
    /// <param name="spans">The parsed spans the analysis text was built from. Must not be null.</param>
    /// <param name="analysis">The analyzer's result, relative to the analysis text. Must not be null.</param>
    /// <returns>
    ///     The input list instance when nothing needs attaching; otherwise a new list in which tag
    ///     spans are the same instances (except a tag span that carries a sentence break, which is
    ///     a copy with <see cref="TaggedTextSpan.SentenceBreaks"/> of <c>[1]</c>) and plain spans
    ///     carry their annotations and (when the
    ///     analysis supplied them) a non-null, possibly empty, sentence-break list.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="spans"/> or <paramref name="analysis"/> is null.</exception>
    internal static IReadOnlyList<TaggedTextSpan> Annotate(IReadOnlyList<TaggedTextSpan> spans, TextAnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(spans);
        ArgumentNullException.ThrowIfNull(analysis);

        // Lay the plain spans out in the analysis text: each plain span owns [start, end), and
        // each tag span owns exactly one placeholder character between them.
        var intervals = new List<(int SpanIndex, int Start, int End)>();
        var tagSpansByPosition = new Dictionary<int, int>();
        var position = 0;
        for (var i = 0; i < spans.Count; i++)
        {
            if (spans[i].Kind == TaggedTextSpanKind.PlainText)
            {
                intervals.Add((i, position, position + spans[i].Text.Length));
                position += spans[i].Text.Length;
            }
            else
            {
                tagSpansByPosition[position] = i;
                position++;
            }
        }

        var analysisLength = position;
        var annotationsBySpan = new Dictionary<int, List<SegmentAnnotation>>();
        var breaksBySpan = new Dictionary<int, List<int>>();
        var tagBreakSpans = new HashSet<int>();

        // An annotation is kept only when it lies wholly inside one plain span; anything that
        // covers a placeholder, crosses spans, or lies outside the text is dropped.
        foreach (var annotation in analysis.Annotations)
        {
            if (annotation.End > analysisLength)
            {
                continue;
            }

            var index = FindInterval(intervals, annotation.Start);
            if (index < 0 || annotation.End > intervals[index].End)
            {
                continue;
            }

            var interval = intervals[index];
            if (!annotationsBySpan.TryGetValue(interval.SpanIndex, out var list))
            {
                list = [];
                annotationsBySpan[interval.SpanIndex] = list;
            }

            list.Add(annotation.Shifted(-interval.Start));
        }

        // Sentence breaks are authoritative when supplied: every plain span then receives a
        // non-null (possibly empty) list so the renderer knows the analyzer spoke for the whole
        // request. A break touching a plain span (including its edges) lands on that span. A
        // break strictly inside a run of tags has no plain text to hold it, so it is attached to
        // the tag span immediately before it (tag offset 1, "after this tag"). A break before a
        // leading tag, or beyond the text, is dropped.
        var breaks = analysis.SentenceBreaks;
        if (breaks is not null)
        {
            foreach (var interval in intervals)
            {
                breaksBySpan[interval.SpanIndex] = [];
            }

            foreach (var offset in breaks)
            {
                var index = FindInterval(intervals, offset);
                if (index >= 0)
                {
                    breaksBySpan[intervals[index].SpanIndex].Add(offset - intervals[index].Start);
                }
                else if (offset >= 1 && offset <= analysisLength && tagSpansByPosition.TryGetValue(offset - 1, out var tagSpanIndex))
                {
                    tagBreakSpans.Add(tagSpanIndex);
                }
            }
        }

        if (annotationsBySpan.Count == 0 && breaks is null)
        {
            return spans;
        }

        var result = new List<TaggedTextSpan>(spans.Count);
        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            if (tagBreakSpans.Contains(i))
            {
                result.Add(span with { SentenceBreaks = [1] });
                continue;
            }
            var hasAnnotations = annotationsBySpan.TryGetValue(i, out var annotations);
            var hasBreaks = breaksBySpan.TryGetValue(i, out var spanBreaks);
            if (!hasAnnotations && !hasBreaks)
            {
                result.Add(span);
                continue;
            }

            result.Add(span with
            {
                Annotations = hasAnnotations ? annotations! : span.Annotations,
                SentenceBreaks = hasBreaks ? spanBreaks : span.SentenceBreaks,
            });
        }

        return result;
    }

    /// <summary>
    ///     Finds the plain-span interval containing an offset of the analysis text, treating each
    ///     interval as closed at both ends.
    /// </summary>
    /// <param name="intervals">The ascending, non-touching plain-span intervals.</param>
    /// <param name="offset">The analysis-text offset to look up.</param>
    /// <returns>The interval index, or -1 when the offset is not inside any plain span.</returns>
    private static int FindInterval(List<(int SpanIndex, int Start, int End)> intervals, int offset)
    {
        var low = 0;
        var high = intervals.Count - 1;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var interval = intervals[mid];
            if (offset < interval.Start)
            {
                high = mid - 1;
            }
            else if (offset > interval.End)
            {
                low = mid + 1;
            }
            else
            {
                return mid;
            }
        }

        return -1;
    }
}
