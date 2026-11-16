using System.Text;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Text.En.NormalizationSubsystem;

/// <summary>
///     What produced one <c>en.norm</c> replacement; reported by the detailed analysis so a
///     caller can tell an abbreviation from a symbol without parsing the spoken text.
/// </summary>
public enum NormalizationKind
{
    /// <summary>
    ///     A lexicon abbreviation (<c>Dr.</c>, <c>etc.</c>), a listed slash form (<c>w/o</c>,
    ///     <c>N/A</c>) or a single-letter initial (<c>J.</c>).
    /// </summary>
    Abbreviation = 1,

    /// <summary>
    ///     An acronym or initialism spoken letter by letter: a listed one (<c>FBI</c>), a dotted
    ///     one (<c>U.S.</c>), an ampersand one (<c>AT&amp;T</c>) or one recognized by rule.
    /// </summary>
    Initialism = 2,

    /// <summary>
    ///     A non-numeric symbol or mark spoken as words (<c>&amp;</c>, <c>@</c>, <c>=</c>, the
    ///     copyright sign), including an <c>&amp;</c> or <c>/</c> inside a word.
    /// </summary>
    Symbol = 3,

    /// <summary>
    ///     A typographic apostrophe replaced by the plain apostrophe (U+0027) inside a clitic or
    ///     an elision such as <c>y’all</c>.
    /// </summary>
    Apostrophe = 4,

    /// <summary>
    ///     An invisible format character or a bullet that is spoken as nothing; the replacement
    ///     text is empty.
    /// </summary>
    Dropped = 5,

    /// <summary>
    ///     A number spoken as words: a cardinal, ordinal, decimal, fraction, year, decade,
    ///     percent sign, <c>#</c> number, range, or a group of digits read one by one. The
    ///     spoken text has no digits.
    /// </summary>
    Number = 6,

    /// <summary>
    ///     A currency amount: the number and its currency unit (<c>$5.50</c>), or the unit word
    ///     of a currency code or magnitude (<c>USD</c>, <c>million</c> after a symbol).
    /// </summary>
    Currency = 7,

    /// <summary>
    ///     A date or time: a clock time, a meridiem, a numeric date, or a day or year written
    ///     next to a month name.
    /// </summary>
    DateTime = 8,

    /// <summary>
    ///     A unit of measure after a number (<c>kg</c>, <c>ft.</c>, <c>&#176;F</c>), spoken in
    ///     full and singular or plural by the number.
    /// </summary>
    Unit = 9,
}

/// <summary>
///     The grammatical role of a clitic token such as <c>'s</c> or <c>n't</c>, recorded by the
///     detailed analysis for later increments (for example pronunciation selection). No
///     annotation is emitted for it.
/// </summary>
public enum CliticKind
{
    /// <summary>The token is not a clitic, or no classification was requested.</summary>
    None = 0,

    /// <summary>A possessive <c>'s</c> (<c>John's book</c>).</summary>
    Possessive = 1,

    /// <summary>A contracted <c>is</c> (<c>it's cold</c>).</summary>
    Is = 2,

    /// <summary>A contracted <c>has</c> (<c>it's been</c>).</summary>
    Has = 3,

    /// <summary>A contracted <c>would</c> (<c>I'd go</c>).</summary>
    Would = 4,

    /// <summary>A contracted <c>had</c> (<c>I'd been</c>).</summary>
    Had = 5,

    /// <summary>A contracted <c>will</c> (<c>we'll go</c>).</summary>
    Will = 6,

    /// <summary>A contracted <c>have</c> (<c>we've gone</c>).</summary>
    Have = 7,

    /// <summary>A contracted <c>are</c> (<c>they're here</c>).</summary>
    Are = 8,

    /// <summary>A contracted <c>am</c> (<c>I'm here</c>).</summary>
    Am = 9,

    /// <summary>A contracted <c>not</c> (<c>don't</c>).</summary>
    Not = 10,

    /// <summary>A contracted <c>us</c> (<c>let's go</c>).</summary>
    Us = 11,
}

/// <summary>
///     One token as the normalizer sees it: where it is in the text and whether it opens a
///     sentence. The normalizer reads the token's characters from the text itself, so no string
///     is allocated per token.
/// </summary>
/// <param name="Start">The UTF-16 offset of the token's first character; not negative.</param>
/// <param name="Length">The number of UTF-16 code units of the token; positive.</param>
/// <param name="StartsSentence">
///     Whether the token is the first token of its tokenizer sentence (the tokenizer's opinion,
///     before any reconciliation).
/// </param>
internal readonly record struct NormalizationToken(int Start, int Length, bool StartsSentence)
{
    /// <summary>
    ///     Gets the exclusive end offset of the token.
    /// </summary>
    internal int End => Start + Length;

    /// <summary>
    ///     Returns the characters of this token within the text it was found in.
    /// </summary>
    /// <param name="text">The text the token indexes. Must contain the whole token.</param>
    /// <returns>A view of the token's characters; nothing is copied.</returns>
    internal ReadOnlySpan<char> In(string text) => text.AsSpan(Start, Length);
}

/// <summary>
///     One replacement span: the exact source characters it covers and how they are spoken.
/// </summary>
/// <param name="Start">The UTF-16 offset of the first covered character.</param>
/// <param name="Length">The number of covered UTF-16 code units; positive.</param>
/// <param name="Spoken">
///     The exact spoken replacement (any single separating space a neighbor needs is already
///     part of it); empty only for <see cref="NormalizationKind.Dropped"/>.
/// </param>
/// <param name="Kind">What produced the replacement.</param>
internal readonly record struct NormalizedSpan(int Start, int Length, string Spoken, NormalizationKind Kind)
{
    /// <summary>
    ///     Gets the exclusive end offset of the span.
    /// </summary>
    internal int End => Start + Length;
}

/// <summary>
///     The classification of one clitic token.
/// </summary>
/// <param name="Start">The UTF-16 offset of the clitic token.</param>
/// <param name="Length">The token's length in UTF-16 code units.</param>
/// <param name="Kind">The clitic role; never <see cref="CliticKind.None"/>.</param>
internal readonly record struct CliticMark(int Start, int Length, CliticKind Kind);

/// <summary>
///     One piece of the full partition of a text into changed spans and untouched gaps.
/// </summary>
/// <param name="Start">The UTF-16 offset of the piece.</param>
/// <param name="Length">The piece's length in UTF-16 code units.</param>
/// <param name="Spoken">The spoken text of a changed span, or the source characters of an untouched gap.</param>
/// <param name="Kind">The kind of a changed span, or <see langword="null"/> for an untouched gap.</param>
internal readonly record struct AlignedSegment(int Start, int Length, string Spoken, NormalizationKind? Kind);

/// <summary>
///     Collects the decisions of one normalization pass and enforces the alignment contract on
///     every one of them, so a defective rule fails loudly here instead of producing annotations
///     a backend would misapply.
/// </summary>
/// <remarks>
///     The contract (invariants A1 to A8 of the design) is: spans are ascending, disjoint and in
///     bounds; every span is non-empty, starts and ends on non-whitespace, never covers a tag
///     placeholder and never cuts a surrogate pair; break edits sit on token starts. The
///     chunk-safety invariant (A5') - no whitespace, never a semicolon, a comma or colon only between
///     two ASCII digits - is guaranteed by the rules that create the spans (the recognizer's span
///     shapes) and verified by the property tests; the builder enforces the structural ones. A builder
///     is single-threaded and used for exactly one pass; it keeps the source text only for the
///     duration of that pass.
/// </remarks>
internal sealed class AlignedTextBuilder
{
    /// <summary>The text the pass normalizes.</summary>
    private readonly string _source;

    /// <summary>The accepted spans, ascending.</summary>
    private readonly List<NormalizedSpan> _spans = [];

    /// <summary>The offsets of breaks to force, in the order requested.</summary>
    private readonly List<int> _forcedBreaks = [];

    /// <summary>The offsets of breaks to suppress, in the order requested.</summary>
    private readonly List<int> _suppressedBreaks = [];

    /// <summary>The accepted clitic classifications, ascending.</summary>
    private readonly List<CliticMark> _clitics = [];

    /// <summary>The end of the last accepted span.</summary>
    private int _lastSpanEnd;

    /// <summary>Whether the spoken text of the last accepted span ends with a letter.</summary>
    private bool _lastSpokenEndsWithLetter;
    /// <summary>The end of the last accepted clitic mark.</summary>
    private int _lastCliticEnd;

    /// <summary>The number of ambiguous resolutions counted so far.</summary>
    private int _ambiguous;

    /// <summary>The number of numeric fallbacks counted so far.</summary>
    private int _fallbacks;

    /// <summary>
    ///     Initializes a new builder for one pass over <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The text being normalized. Must not be null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    internal AlignedTextBuilder(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>
    ///     Adds one replacement span after checking it against the alignment contract.
    /// </summary>
    /// <param name="span">The span to add.</param>
    /// <exception cref="ArgumentException">
    ///     The span is empty, out of bounds, out of order or overlapping the previous span; starts
    ///     or ends on whitespace; covers a tag placeholder; cuts a surrogate pair; has a null or
    ///     placeholder-containing replacement; or has an empty replacement without being
    ///     <see cref="NormalizationKind.Dropped"/> (or a non-empty one while being dropped).
    /// </exception>
    internal void Add(NormalizedSpan span)
    {
        // A1: in bounds, ascending and disjoint
        if (span.Length <= 0 || span.Start < 0 || span.Start > _source.Length || span.Length > _source.Length - span.Start)
        {
            throw new ArgumentException("The span is empty or outside the text.", nameof(span));
        }

        if (span.Start < _lastSpanEnd)
        {
            throw new ArgumentException("The span overlaps or precedes the previous span.", nameof(span));
        }

        // A2: whitespace-free edges, no placeholder inside, no cut surrogate pair
        if (char.IsWhiteSpace(_source[span.Start]) || char.IsWhiteSpace(_source[span.End - 1]))
        {
            throw new ArgumentException("The span starts or ends on whitespace.", nameof(span));
        }

        if (_source.AsSpan(span.Start, span.Length).Contains(TextAnalysisConventions.TagPlaceholder))
        {
            throw new ArgumentException("The span covers a tag placeholder.", nameof(span));
        }

        var startsInsidePair = span.Start > 0 && char.IsLowSurrogate(_source[span.Start]) && char.IsHighSurrogate(_source[span.Start - 1]);
        var endsInsidePair = span.End < _source.Length && char.IsHighSurrogate(_source[span.End - 1]) && char.IsLowSurrogate(_source[span.End]);
        if (startsInsidePair || endsInsidePair)
        {
            throw new ArgumentException("The span cuts a surrogate pair.", nameof(span));
        }

        // A10: the replacement is empty exactly for a dropped span and never carries a placeholder
        if (span.Spoken is null)
        {
            throw new ArgumentException("The spoken text must not be null.", nameof(span));
        }

        if (span.Spoken.Contains(TextAnalysisConventions.TagPlaceholder))
        {
            throw new ArgumentException("The spoken text must not contain a tag placeholder.", nameof(span));
        }

        if ((span.Kind == NormalizationKind.Dropped) != (span.Spoken.Length == 0))
        {
            throw new ArgumentException("Only a dropped span has an empty spoken text.", nameof(span));
        }

        _spans.Add(span);
        _lastSpanEnd = span.End;
        _lastSpokenEndsWithLetter = span.Spoken.Length > 0 && char.IsLetter(span.Spoken[^1]);
    }

    /// <summary>
    ///     Tells whether the previous accepted span ends exactly at <paramref name="start"/> and
    ///     its spoken text ends with a letter, so a following replacement that starts with a
    ///     letter needs a separating space to avoid running the two words together.
    /// </summary>
    /// <param name="start">The start of the span about to be added.</param>
    /// <returns><see langword="true"/> when the previous span directly touches <paramref name="start"/> and ends with a letter.</returns>
    internal bool TouchesPreviousSpokenLetter(int start) => _lastSpokenEndsWithLetter && start == _lastSpanEnd;

    /// <summary>
    ///     Requests a sentence break immediately before the token that starts at
    ///     <paramref name="offset"/>.
    /// </summary>
    /// <param name="offset">The offset of the token's first character.</param>
    /// <exception cref="ArgumentOutOfRangeException">The offset is not inside the text after its first character, or is not a token start (whitespace or a placeholder).</exception>
    internal void ForceBreak(int offset)
    {
        RequireBreakOffset(offset);
        _forcedBreaks.Add(offset);
    }

    /// <summary>
    ///     Requests that the tokenizer's sentence break immediately before the token that starts
    ///     at <paramref name="offset"/> be removed.
    /// </summary>
    /// <param name="offset">The offset of the token's first character.</param>
    /// <exception cref="ArgumentOutOfRangeException">The offset is not inside the text after its first character, or is not a token start (whitespace or a placeholder).</exception>
    internal void SuppressBreak(int offset)
    {
        RequireBreakOffset(offset);
        _suppressedBreaks.Add(offset);
    }

    /// <summary>
    ///     Records the classification of one clitic token.
    /// </summary>
    /// <param name="mark">The classification.</param>
    /// <exception cref="ArgumentException">The token is out of bounds, out of order, empty or classified <see cref="CliticKind.None"/>.</exception>
    internal void MarkClitic(CliticMark mark)
    {
        // The range is validated without computing Start + Length, which can overflow for a
        // hostile mark and wrap around to look in bounds: Start is checked against the text
        // length first (a negative Start is already rejected by the ordering check, because
        // _lastCliticEnd is never negative) and Length against what remains after it.
        if (mark.Length <= 0
            || mark.Start < _lastCliticEnd
            || mark.Start > _source.Length
            || mark.Length > _source.Length - mark.Start
            || mark.Kind == CliticKind.None)
        {
            throw new ArgumentException("The clitic mark is empty, out of order, out of bounds or unclassified.", nameof(mark));
        }

        _clitics.Add(mark);
        _lastCliticEnd = mark.Start + mark.Length;
    }

    /// <summary>
    ///     Counts one resolution that fell back to a default or chose between two competing cues.
    /// </summary>
    internal void CountAmbiguous() => _ambiguous++;

    /// <summary>
    ///     Counts one numeric-looking token or expression that was left as written, or read digit
    ///     by digit, because it is beyond the documented size limits or is not an en-US shape.
    /// </summary>
    internal void CountFallback() => _fallbacks++;

    /// <summary>
    ///     Finishes the pass.
    /// </summary>
    /// <returns>The immutable aligned text; the builder should not be used afterwards.</returns>
    internal AlignedText Build()
    {
        // Break edits may be requested in any order (a later rule can name an earlier token), so
        // they are sorted and de-duplicated here; spans and clitics were ordered on entry.
        return new AlignedText(
            _source,
            _spans.ToArray(),
            SortedDistinct(_forcedBreaks),
            SortedDistinct(_suppressedBreaks),
            _clitics.ToArray(),
            _ambiguous,
            _fallbacks);
    }

    /// <summary>
    ///     Validates the offset of a break edit.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <exception cref="ArgumentOutOfRangeException">The offset is outside [1, length - 1] or names whitespace or a tag placeholder.</exception>
    private void RequireBreakOffset(int offset)
    {
        if (offset < 1 || offset >= _source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "A break must lie inside the text, after its first character.");
        }

        var character = _source[offset];
        if (char.IsWhiteSpace(character) || character == TextAnalysisConventions.TagPlaceholder)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "A break must sit on the first character of a token.");
        }
    }

    /// <summary>
    ///     Returns the values ascending with duplicates removed.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>A new sorted, duplicate-free array.</returns>
    private static int[] SortedDistinct(List<int> values)
    {
        var sorted = values.ToArray();
        Array.Sort(sorted);
        var count = 0;
        for (var i = 0; i < sorted.Length; i++)
        {
            if (i == 0 || sorted[i] != sorted[i - 1])
            {
                sorted[count++] = sorted[i];
            }
        }

        return count == sorted.Length ? sorted : sorted[..count];
    }
}

/// <summary>
///     The outcome of one normalization pass over a text: which spans are spoken differently,
///     which sentence breaks the normalization adds or removes, and the clitic roles found.
/// </summary>
/// <remarks>
///     The text itself is never rewritten: <see cref="Spans"/> only name source ranges and
///     their spoken replacements, and everything outside them is spoken as written. Instances
///     are immutable and thread-safe.
/// </remarks>
internal sealed class AlignedText
{
    /// <summary>
    ///     Initializes a new instance from a builder's already validated, ordered data.
    /// </summary>
    /// <param name="source">The normalized text.</param>
    /// <param name="spans">The changed spans, ascending and disjoint.</param>
    /// <param name="forcedBreaks">The forced break offsets, ascending and unique.</param>
    /// <param name="suppressedBreaks">The suppressed break offsets, ascending and unique.</param>
    /// <param name="clitics">The clitic classifications, ascending.</param>
    /// <param name="ambiguousCount">The number of ambiguous resolutions.</param>
    /// <param name="fallbackCount">The number of numeric fallbacks; 0 when omitted.</param>
    internal AlignedText(
        string source,
        IReadOnlyList<NormalizedSpan> spans,
        IReadOnlyList<int> forcedBreaks,
        IReadOnlyList<int> suppressedBreaks,
        IReadOnlyList<CliticMark> clitics,
        int ambiguousCount,
        int fallbackCount = 0)
    {
        Source = source;
        Spans = spans;
        ForcedBreaks = forcedBreaks;
        SuppressedBreaks = suppressedBreaks;
        Clitics = clitics;
        AmbiguousCount = ambiguousCount;
        FallbackCount = fallbackCount;
    }

    /// <summary>Gets the normalized text, exactly as it was supplied.</summary>
    internal string Source { get; }

    /// <summary>Gets the changed spans only, ascending and disjoint; identity text is never listed.</summary>
    internal IReadOnlyList<NormalizedSpan> Spans { get; }

    /// <summary>Gets the offsets of tokens that must start a sentence, ascending and unique.</summary>
    internal IReadOnlyList<int> ForcedBreaks { get; }

    /// <summary>Gets the offsets of tokens that must not start a sentence, ascending and unique.</summary>
    internal IReadOnlyList<int> SuppressedBreaks { get; }

    /// <summary>Gets the clitic classifications; empty unless classification was requested.</summary>
    internal IReadOnlyList<CliticMark> Clitics { get; }

    /// <summary>Gets the number of resolutions that used a default or chose between competing cues.</summary>
    internal int AmbiguousCount { get; }

    /// <summary>Gets the number of numeric expressions left as written or read digit by digit because of a documented limit or an unsupported numeric shape.</summary>
    internal int FallbackCount { get; }

    /// <summary>
    ///     Enumerates the full partition of the text: every changed span and every untouched gap
    ///     between them, in order, contiguous and covering the whole text.
    /// </summary>
    /// <returns>The segments; empty for an empty text.</returns>
    internal IEnumerable<AlignedSegment> Segments()
    {
        var position = 0;
        foreach (var span in Spans)
        {
            // The untouched text before this span, if any.
            if (span.Start > position)
            {
                yield return new AlignedSegment(position, span.Start - position, Source.Substring(position, span.Start - position), null);
            }

            yield return new AlignedSegment(span.Start, span.Length, span.Spoken, span.Kind);
            position = span.End;
        }

        // The untouched tail.
        if (position < Source.Length)
        {
            yield return new AlignedSegment(position, Source.Length - position, Source[position..], null);
        }
    }

    /// <summary>
    ///     Applies every span: the text a backend would speak.
    /// </summary>
    /// <returns>The source with each span replaced by its spoken text.</returns>
    internal string ApplySpokenForms()
    {
        var builder = new StringBuilder(Source.Length + 16);
        var position = 0;
        foreach (var span in Spans)
        {
            builder.Append(Source, position, span.Start - position);
            builder.Append(span.Spoken);
            position = span.End;
        }

        builder.Append(Source, position, Source.Length - position);
        return builder.ToString();
    }

    /// <summary>
    ///     Combines the tokenizer's sentence breaks with this normalization's edits: every
    ///     tokenizer break and every forced break, except the suppressed ones.
    /// </summary>
    /// <param name="tokenizerBreaks">The tokenizer's break offsets, ascending. Must not be null.</param>
    /// <returns>The reconciled breaks, ascending and unique.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tokenizerBreaks"/> is null.</exception>
    internal IReadOnlyList<int> ReconcileBreaks(IReadOnlyList<int> tokenizerBreaks)
    {
        ArgumentNullException.ThrowIfNull(tokenizerBreaks);

        // Merge two ascending sequences, dropping duplicates and suppressed offsets as they go.
        var result = new List<int>(tokenizerBreaks.Count + ForcedBreaks.Count);
        var suppressed = SuppressedBreaks;
        int a = 0, b = 0;
        while (a < tokenizerBreaks.Count || b < ForcedBreaks.Count)
        {
            int next;
            if (b >= ForcedBreaks.Count || (a < tokenizerBreaks.Count && tokenizerBreaks[a] <= ForcedBreaks[b]))
            {
                next = tokenizerBreaks[a];
                a++;
            }
            else
            {
                next = ForcedBreaks[b];
                b++;
            }

            if ((result.Count == 0 || result[^1] != next) && !IsSuppressed(suppressed, next))
            {
                result.Add(next);
            }
        }

        return result.AsReadOnly();
    }

    /// <summary>
    ///     Tells whether an offset is in the ascending suppressed list.
    /// </summary>
    /// <param name="suppressed">The ascending suppressed offsets.</param>
    /// <param name="offset">The offset to look for.</param>
    /// <returns><see langword="true"/> when the offset is listed.</returns>
    private static bool IsSuppressed(IReadOnlyList<int> suppressed, int offset)
    {
        // Binary search over the read-only list (the list is small, but its size is not bounded).
        int low = 0, high = suppressed.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var value = suppressed[middle];
            if (value == offset)
            {
                return true;
            }

            if (value < offset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return false;
    }
}
