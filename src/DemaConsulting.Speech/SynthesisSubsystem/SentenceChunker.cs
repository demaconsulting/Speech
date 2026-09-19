namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Splits one run of plain synthesis text into sentence/clause-sized chunks, so no single
///     synthesis call is so long that streaming, pipelined playback loses its low-latency feel.
/// </summary>
/// <remarks>
///     Splits primarily on sentence-ending punctuation (<c>.</c>, <c>!</c>, <c>?</c>). A maximal
///     run of consecutive sentence-ending characters (in any combination, e.g. an ellipsis
///     <c>...</c>, or mixed terminators such as <c>?!</c> or <c>!!</c>) is treated as a single
///     boundary: the split happens only once, after the last character of the run, and the whole
///     run stays attached to the sentence that precedes it. Every resulting sentence-level piece
///     is then <em>always</em> split further on clause punctuation (<c>,</c>, <c>;</c>, <c>:</c>),
///     unconditionally - not only when the piece exceeds the length budget - so every clause
///     becomes its own chunk, which keeps the audible gap at a clause boundary short and
///     predictable regardless of overall sentence length. A punctuation character flanked by a
///     digit on both sides (e.g. the <c>:</c> in <c>"12:30"</c> or the <c>,</c> in <c>"1,000"</c>)
///     is never treated as a boundary at either pass, so numerals, times, and similar digit
///     groups are never split apart. A decimal point immediately followed by a digit is also
///     never treated as a boundary even without a leading digit (e.g. the <c>.</c> in a
///     bare-fraction decimal such as <c>".5"</c> or <c>"$.99"</c>), so a leading decimal point is
///     never mistaken for a sentence-ending period and dropped, which would otherwise silence the
///     "point" when the number is spoken. A piece still too long after both punctuation passes is
///     split further on whitespace boundaries so no chunk exceeds the budget by more than one
///     word. Finally, a piece produced by either punctuation pass whose trimmed text contains no
///     letter or digit at all (i.e. it is nothing but punctuation and/or whitespace, such as a
///     lone comma or a whitespace-spaced run of dots) is never emitted as its own chunk: its
///     punctuation is instead appended to the immediately preceding non-empty chunk, or dropped
///     entirely if there is no preceding chunk (a degenerate run at the very start of the text).
///     This is a best-effort UX quality heuristic, not a correctness requirement: an oversized or
///     undersized chunk still synthesizes and plays correctly.
/// </remarks>
/// <remarks>
///     The numeral exceptions above assume English/US-style numeral punctuation, where <c>,</c>
///     is the thousands separator and <c>.</c> is the decimal point (e.g. <c>"1,000.5"</c>).
///     Locales that swap these roles (e.g. many European locales, where <c>.</c> is the
///     thousands separator and <c>,</c> is the decimal point, as in <c>"1.000,5"</c>) are not
///     specially handled: text written in that convention would have its thousands-separator
///     <c>.</c> correctly kept attached (it is still digit-flanked on both sides), but its
///     decimal-point <c>,</c> would be treated as an ordinary, always-splitting clause boundary,
///     not a numeral exception, splitting the fractional part into its own chunk. Every model in
///     this library's current catalog is English-only, so this is not currently a defect, but a
///     future non-English model or locale-aware caller would need to adjust these rules (e.g.
///     swap which of <c>,</c>/<c>.</c> gets the one-sided, no-leading-digit exception) rather
///     than assuming this class's heuristics generalize as-is.
/// </remarks>
internal static class SentenceChunker
{
    /// <summary>
    ///     The default maximum number of characters a chunk should contain before the
    ///     whitespace-budget fallback split is attempted.
    /// </summary>
    internal const int DefaultMaxChunkLength = 200;

    /// <summary>Primary, sentence-ending split boundaries.</summary>
    private static readonly char[] PrimaryBoundaries = ['.', '!', '?'];

    /// <summary>
    ///     Secondary, clause-level split boundaries. Unlike <see cref="PrimaryBoundaries"/>,
    ///     these are applied unconditionally to every primary piece, regardless of the length
    ///     budget, so every clause becomes its own chunk.
    /// </summary>
    private static readonly char[] SecondaryBoundaries = [',', ';', ':'];

    /// <summary>
    ///     Splits <paramref name="text"/> into an ordered list of sentence/clause-sized chunks.
    /// </summary>
    /// <param name="text">The plain text to chunk. Must not be <see langword="null"/>.</param>
    /// <param name="maxChunkLength">
    ///     The length budget, in characters, past which a chunk is split further on whitespace.
    ///     Must be greater than zero. Defaults to <see cref="DefaultMaxChunkLength"/>.
    /// </param>
    /// <returns>
    ///     An ordered, read-only list of non-empty, trimmed chunks that reconstruct
    ///     <paramref name="text"/>'s words and punctuation in order, except a degenerate,
    ///     word-less punctuation run at the very start of the text (e.g. a stray leading comma),
    ///     which is dropped rather than emitted as its own chunk - see
    ///     <see cref="MergeDegeneratePunctuationPieces"/>. Empty when <paramref name="text"/> is
    ///     empty or all whitespace.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="maxChunkLength"/> is less than or equal to zero.
    /// </exception>
    internal static IReadOnlyList<string> Chunk(string text, int maxChunkLength = DefaultMaxChunkLength) =>
        ChunkWithMetadata(text, maxChunkLength).Select(chunk => chunk.Text).ToArray();

    /// <summary>
    ///     Splits <paramref name="text"/> into an ordered list of sentence/clause-sized chunks,
    ///     same as <see cref="Chunk"/>, but additionally reports whether each chunk ends in a
    ///     genuine ellipsis (see <see cref="SentenceChunk.EndsWithEllipsis"/>), so a caller can
    ///     render a longer pause there.
    /// </summary>
    /// <param name="text">The plain text to chunk. Must not be <see langword="null"/>.</param>
    /// <param name="maxChunkLength">
    ///     The length budget, in characters, past which a chunk is split further on whitespace.
    ///     Must be greater than zero. Defaults to <see cref="DefaultMaxChunkLength"/>.
    /// </param>
    /// <returns>
    ///     An ordered, read-only list of non-empty, trimmed chunks with ellipsis metadata, that
    ///     reconstruct <paramref name="text"/>'s words and punctuation in order, except a
    ///     degenerate, word-less punctuation run at the very start of the text (e.g. a stray
    ///     leading comma), which is dropped rather than emitted as its own chunk - see
    ///     <see cref="MergeDegeneratePunctuationPieces"/>. Empty when <paramref name="text"/> is
    ///     empty or all whitespace.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="maxChunkLength"/> is less than or equal to zero.
    /// </exception>
    internal static IReadOnlyList<SentenceChunk> ChunkWithMetadata(string text, int maxChunkLength = DefaultMaxChunkLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxChunkLength, 0);

        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        var pieces = new List<string>();
        foreach (var primaryPiece in SplitOnBoundaries(trimmed, PrimaryBoundaries, mergeConsecutive: true))
        {
            foreach (var secondaryPiece in SplitOnBoundaries(primaryPiece, SecondaryBoundaries))
            {
                if (secondaryPiece.Length <= maxChunkLength)
                {
                    pieces.Add(secondaryPiece);
                    continue;
                }

                pieces.AddRange(SplitOnWhitespaceBudget(secondaryPiece, maxChunkLength));
            }
        }

        var merged = MergeDegeneratePunctuationPieces(pieces);
        return merged.Select(piece => new SentenceChunk(piece, EndsWithEllipsis(piece))).ToArray();
    }

    /// <summary>
    ///     Merges every piece whose trimmed text contains no letter or digit character at all
    ///     (just punctuation and/or whitespace, e.g. a lone comma or a whitespace-spaced run of
    ///     dots) onto the end of the immediately preceding non-empty piece, joined by a single
    ///     space; drops such a piece entirely when there is no preceding piece to merge into (a
    ///     degenerate run at the very start of the text).
    /// </summary>
    /// <param name="pieces">The ordered, already-split, non-empty pieces to merge.</param>
    /// <returns>An ordered list of pieces with no degenerate, word-less entries.</returns>
    private static List<string> MergeDegeneratePunctuationPieces(IReadOnlyList<string> pieces)
    {
        var merged = new List<string>();
        foreach (var piece in pieces)
        {
            if (HasWordContent(piece))
            {
                merged.Add(piece);
                continue;
            }

            if (merged.Count > 0)
            {
                merged[^1] = $"{merged[^1]} {piece}";
            }

            // No preceding piece to merge into: a leading degenerate run is dropped.
        }

        return merged;
    }

    /// <summary>
    ///     Determines whether <paramref name="text"/> contains at least one letter or digit
    ///     character, i.e. is not purely punctuation and/or whitespace.
    /// </summary>
    /// <param name="text">The text to inspect.</param>
    /// <returns><see langword="true"/> when at least one letter or digit is present.</returns>
    private static bool HasWordContent(string text) => text.Any(char.IsLetterOrDigit);

    /// <summary>
    ///     Determines whether <paramref name="text"/> ends in a genuine ellipsis: three or more
    ///     consecutive <c>.</c> characters, optionally interspersed with whitespace (so both
    ///     <c>"..."</c> and <c>". . ."</c>/<c>". . . . ."</c> count), found by scanning backward
    ///     from the end of the text.
    /// </summary>
    /// <param name="text">The final, already-merged chunk text to inspect.</param>
    /// <returns><see langword="true"/> when the chunk ends in a genuine ellipsis.</returns>
    private static bool EndsWithEllipsis(string text)
    {
        var dotCount = 0;
        for (var i = text.Length - 1; i >= 0; i--)
        {
            var c = text[i];
            if (c == '.')
            {
                dotCount++;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            break;
        }

        return dotCount >= 3;
    }

    /// <summary>
    ///     Splits text immediately after each occurrence of any boundary character, keeping the
    ///     boundary attached to the piece that precedes it.
    /// </summary>
    /// <param name="text">The trimmed text to split.</param>
    /// <param name="boundaries">The characters to split after.</param>
    /// <param name="mergeConsecutive">
    ///     When <see langword="true"/>, a maximal run of consecutive boundary characters (in any
    ///     combination drawn from <paramref name="boundaries"/>, e.g. an ellipsis <c>...</c> or a
    ///     mixed terminator such as <c>?!</c>) is treated as a single boundary: the split happens
    ///     only once, after the last character of the run. When <see langword="false"/> (the
    ///     default), every occurrence of a boundary character splits individually, even when
    ///     immediately adjacent to another boundary character.
    /// </param>
    /// <returns>An ordered list of non-empty, trimmed pieces.</returns>
    private static List<string> SplitOnBoundaries(string text, char[] boundaries, bool mergeConsecutive = false)
    {
        var pieces = new List<string>();
        var start = 0;
        var i = 0;
        while (i < text.Length)
        {
            if (Array.IndexOf(boundaries, text[i]) < 0)
            {
                i++;
                continue;
            }

            // A boundary character flanked by digits on both sides (e.g. the ":" in "12:30" or
            // the "," in "1,000") is part of a numeral, not a clause/sentence boundary - leave it
            // attached to its surrounding digits rather than splitting.
            if (IsDigitFlanked(text, i))
            {
                i++;
                continue;
            }

            // Extend over a maximal run of consecutive boundary characters when requested, so
            // e.g. an ellipsis ("...") or a mixed terminator ("?!") splits once, not repeatedly.
            var end = i;
            if (mergeConsecutive)
            {
                while (end + 1 < text.Length && Array.IndexOf(boundaries, text[end + 1]) >= 0)
                {
                    end++;
                }
            }

            var piece = text[start..(end + 1)].Trim();
            if (piece.Length > 0)
            {
                pieces.Add(piece);
            }

            start = end + 1;
            i = end + 1;
        }

        var remainder = text[start..].Trim();
        if (remainder.Length > 0)
        {
            pieces.Add(remainder);
        }

        return pieces;
    }

    /// <summary>
    ///     Determines whether the character at <paramref name="index"/> is embedded inside a
    ///     numeral rather than acting as a clause/sentence separator: either a digit both
    ///     immediately before and immediately after it (e.g. the <c>:</c> in <c>"12:30"</c> or
    ///     the <c>,</c> in <c>"1,000"</c>), or - for a decimal point only - a digit immediately
    ///     after with no letter immediately before (e.g. the leading <c>.</c> in a bare-fraction
    ///     decimal such as <c>".5"</c> or <c>"$.99"</c>, which has no leading digit to flank it on
    ///     the left). Without this second case, a leading decimal point is indistinguishable from
    ///     a sentence-ending period and gets dropped as a degenerate, word-less chunk, silencing
    ///     the "point" when the number is later spoken (e.g. ".5" reads as "five" instead of
    ///     "point five").
    /// </summary>
    /// <param name="text">The text being scanned.</param>
    /// <param name="index">The index of the boundary character to check.</param>
    /// <returns><see langword="true"/> when the character is part of a numeral.</returns>
    private static bool IsDigitFlanked(string text, int index)
    {
        if (index + 1 >= text.Length || !char.IsDigit(text[index + 1]))
        {
            return false;
        }

        if (index > 0 && char.IsDigit(text[index - 1]))
        {
            return true;
        }

        return text[index] == '.' && (index == 0 || !char.IsLetter(text[index - 1]));
    }

    /// <summary>
    ///     Splits text on whitespace boundaries so that no returned piece exceeds
    ///     <paramref name="maxChunkLength"/>, except a single word that alone exceeds the budget
    ///     (which is returned whole rather than broken mid-word).
    /// </summary>
    /// <param name="text">The text to split, still longer than the budget as a whole.</param>
    /// <param name="maxChunkLength">The length budget, in characters.</param>
    /// <returns>An ordered list of non-empty pieces.</returns>
    private static List<string> SplitOnWhitespaceBudget(string text, int maxChunkLength)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var pieces = new List<string>();
        var current = new List<string>();
        var currentLength = 0;

        foreach (var word in words)
        {
            var additionalLength = word.Length + (current.Count > 0 ? 1 : 0);
            if (current.Count > 0 && currentLength + additionalLength > maxChunkLength)
            {
                pieces.Add(string.Join(' ', current));
                current.Clear();
                currentLength = 0;
                additionalLength = word.Length;
            }

            current.Add(word);
            currentLength += additionalLength;
        }

        if (current.Count > 0)
        {
            pieces.Add(string.Join(' ', current));
        }

        return pieces;
    }
}

/// <summary>
///     One chunk produced by <see cref="SentenceChunker.ChunkWithMetadata"/>: its text, plus
///     whether it ends in a genuine ellipsis so a caller can render a longer pause there.
/// </summary>
/// <param name="Text">The chunk's non-empty, trimmed text.</param>
/// <param name="EndsWithEllipsis">
///     <see langword="true"/> when <paramref name="Text"/> ends in a genuine ellipsis - three or
///     more consecutive <c>.</c> characters, with or without interspersed whitespace (e.g.
///     <c>"..."</c> or <c>". . ."</c>) - which should render a longer pause than an ordinary
///     sentence-ending <c>.</c>/<c>!</c>/<c>?</c>.
/// </param>
internal readonly record struct SentenceChunk(string Text, bool EndsWithEllipsis);
