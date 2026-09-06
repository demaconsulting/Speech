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
///     run stays attached to the sentence that precedes it. A chunk that is still longer than the
///     length budget after that pass is split further on clause punctuation (<c>,</c>, <c>;</c>,
///     <c>:</c>), where each occurrence still splits individually; a chunk still too long after
///     both passes is split on whitespace boundaries so no chunk exceeds the budget by more than
///     one word. This is a best-effort UX quality heuristic, not a correctness requirement: an
///     oversized or undersized chunk still synthesizes and plays correctly.
/// </remarks>
internal static class SentenceChunker
{
    /// <summary>
    ///     The default maximum number of characters a chunk should contain before the secondary
    ///     clause-boundary split is attempted.
    /// </summary>
    internal const int DefaultMaxChunkLength = 200;

    /// <summary>Primary, sentence-ending split boundaries.</summary>
    private static readonly char[] PrimaryBoundaries = ['.', '!', '?'];

    /// <summary>Secondary, clause-level split boundaries, used only past the length budget.</summary>
    private static readonly char[] SecondaryBoundaries = [',', ';', ':'];

    /// <summary>
    ///     Splits <paramref name="text"/> into an ordered list of sentence/clause-sized chunks.
    /// </summary>
    /// <param name="text">The plain text to chunk. Must not be <see langword="null"/>.</param>
    /// <param name="maxChunkLength">
    ///     The length budget, in characters, past which a chunk is split further. Must be greater
    ///     than zero. Defaults to <see cref="DefaultMaxChunkLength"/>.
    /// </param>
    /// <returns>
    ///     An ordered, read-only list of non-empty, trimmed chunks that reconstruct
    ///     <paramref name="text"/>'s words and punctuation in order. Empty when
    ///     <paramref name="text"/> is empty or all whitespace.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="maxChunkLength"/> is less than or equal to zero.
    /// </exception>
    internal static IReadOnlyList<string> Chunk(string text, int maxChunkLength = DefaultMaxChunkLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxChunkLength, 0);

        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        var chunks = new List<string>();
        foreach (var primaryPiece in SplitOnBoundaries(trimmed, PrimaryBoundaries, mergeConsecutive: true))
        {
            if (primaryPiece.Length <= maxChunkLength)
            {
                chunks.Add(primaryPiece);
                continue;
            }

            foreach (var secondaryPiece in SplitOnBoundaries(primaryPiece, SecondaryBoundaries))
            {
                if (secondaryPiece.Length <= maxChunkLength)
                {
                    chunks.Add(secondaryPiece);
                    continue;
                }

                chunks.AddRange(SplitOnWhitespaceBudget(secondaryPiece, maxChunkLength));
            }
        }

        return chunks;
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
