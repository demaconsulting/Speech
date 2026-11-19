using System.Text;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Splits one run of plain synthesis text into sentence/clause-sized chunks, so no single
///     synthesis call is so long that streaming playback loses its low-latency feel.
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
///     never treated as a boundary even without a leading digit, provided no letter immediately
///     precedes it (e.g. the <c>.</c> in a bare-fraction decimal such as <c>".5"</c> or
///     <c>"$.99"</c>, at start-of-text, after whitespace, a sign, or a currency symbol), so a
///     leading decimal point is never mistaken for a sentence-ending period and dropped, which
///     would otherwise silence the "point" when the number is spoken. A period directly preceded
///     by a letter (e.g. after an abbreviation-like word, as in <c>"Wait.5 more."</c>) does not
///     qualify for this exception and still splits as an ordinary sentence boundary. A piece
///     still too long after both punctuation passes is
///     split further on whitespace boundaries, preferably where a writer would pause: after a dash,
///     around a bracket, before a conjunction or relative word (<c>and</c>, <c>that</c>, <c>which</c>),
///     and otherwise after a word that does not need the word after it (never after <c>of</c>, <c>the</c>,
///     <c>we</c> and the like), so no chunk exceeds the budget unless it is a single
///     word longer than the budget, which is emitted unsplit. Finally, a piece produced by either punctuation pass whose trimmed text contains no
///     letter or digit at all (i.e. it is nothing but punctuation and/or whitespace, such as a
///     lone comma or a whitespace-spaced run of dots) is never emitted as its own chunk: its
///     punctuation is instead appended to the immediately preceding non-empty chunk, or dropped
///     entirely if there is no preceding chunk (a degenerate run at the very start of the text).
///     The one exception applies only when annotations are supplied: such a piece that wholly
///     contains at least one annotation with a non-empty value (for example a symbol that an
///     analyzer normalized to a spoken word) is content, so it is kept as its own chunk with its
///     annotation instead of being merged or dropped. An annotation with an empty value, or one
///     that crosses into the piece, does not keep it.
///     This is a best-effort UX quality heuristic, not a correctness requirement: an oversized or
///     undersized chunk still synthesizes and plays correctly.
///     <para>
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
///     </para>
///     <para>
///     The class is public so a model package can build its own tag-rendering strategy on the
///     library's chunking instead of copying it. <see cref="ChunkAligned"/> additionally carries
///     text-analysis <see cref="SegmentAnnotation"/>s through the chunking (re-basing their
///     offsets onto each chunk, and dropping an annotation that cannot be placed wholly inside
///     one chunk) and lets an analyzer supply authoritative sentence boundaries in place of the
///     sentence-punctuation heuristic. One position-aware implementation serves every entry
///     point, so unannotated output is identical to the output before annotations existed.
///     The type is stateless and thread-safe.
///     </para>
/// </remarks>
public static class SentenceChunker
{
    /// <summary>
    ///     The default maximum number of characters a chunk should contain before the
    ///     whitespace-budget fallback split is attempted.
    /// </summary>
    public const int DefaultMaxChunkLength = 200;

    /// <summary>Primary, sentence-ending split boundaries.</summary>
    private static readonly char[] PrimaryBoundaries = ['.', '!', '?'];

    /// <summary>
    ///     Secondary, clause-level split boundaries. Unlike <see cref="PrimaryBoundaries"/>,
    ///     these are applied unconditionally to every primary piece, regardless of the length
    ///     budget, so every clause becomes its own chunk.
    /// </summary>
    private static readonly char[] SecondaryBoundaries = [',', ';', ':'];

    /// <summary>
    ///     The English words that start a clause: a piece that has to be cut inside a long clause may end before one of them.
    /// </summary>
    private static readonly HashSet<string> ClauseStarters = new(
        [
            "and", "but", "or", "nor", "yet", "so", "that", "which", "who", "whom", "whose", "because", "although", "though",
            "while", "whereas", "when", "whenever", "where", "wherever", "if", "unless", "until", "since", "than", "whether",
        ],
        StringComparer.Ordinal);

    /// <summary>
    ///     The English function words (articles, prepositions, auxiliaries, pronouns and negation) a piece is not ended after
    ///     when it can be helped: each of them needs the word that follows it (<i>of | devotion</i>, <i>we | here</i>).
    /// </summary>
    private static readonly HashSet<string> FunctionWords = new(
        [
            "a", "an", "the", "this", "these", "those", "my", "your", "his", "her", "its", "our", "their", "some", "any", "no", "every", "each",
            "of", "in", "on", "at", "by", "for", "with", "from", "to", "into", "onto", "upon", "over", "under", "about", "as", "through",
            "between", "among", "against", "during", "without", "within", "across", "after", "before", "per", "via",
            "is", "are", "was", "were", "be", "been", "being", "am", "have", "has", "had", "do", "does", "did", "will", "would", "shall",
            "should", "can", "could", "may", "might", "must", "not", "i", "you", "he", "she", "it", "we", "they", "me", "him", "us", "them",
        ],
        StringComparer.Ordinal);

    /// <summary>
    ///     A half-open range of the original source text.
    /// </summary>
    /// <param name="Start">The inclusive start index.</param>
    /// <param name="End">The exclusive end index.</param>
    private readonly record struct SourceRange(int Start, int End)
    {
        /// <summary>Gets the number of characters in the range.</summary>
        public int Length => End - Start;
    }

    /// <summary>
    ///     Records that <c>Length</c> characters of a piece's text, starting at
    ///     <c>TextStart</c>, are copied verbatim from the original source starting at
    ///     <c>SourceStart</c>.
    /// </summary>
    /// <param name="SourceStart">The index in the original source text.</param>
    /// <param name="Length">The number of copied characters.</param>
    /// <param name="TextStart">The index in the piece's own text.</param>
    private readonly record struct SourceRun(int SourceStart, int Length, int TextStart);

    /// <summary>
    ///     An in-progress chunk: its final text plus the runs mapping it back to the original
    ///     source, needed because a piece is not always a plain substring (whitespace is trimmed,
    ///     collapsed when a budget split re-joins words, and punctuation-only pieces are merged).
    /// </summary>
    /// <param name="Text">The piece's final text.</param>
    /// <param name="Runs">The ascending source runs that make up the text.</param>
    private readonly record struct Piece(string Text, SourceRun[] Runs);

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
    ///     which is dropped rather than emitted as its own chunk. Empty when
    ///     <paramref name="text"/> is empty or all whitespace.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="maxChunkLength"/> is less than or equal to zero.
    /// </exception>
    public static IReadOnlyList<string> Chunk(string text, int maxChunkLength = DefaultMaxChunkLength) =>
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
    ///     leading comma), which is dropped rather than emitted as its own chunk. Empty when
    ///     <paramref name="text"/> is empty or all whitespace.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="maxChunkLength"/> is less than or equal to zero.
    /// </exception>
    public static IReadOnlyList<SentenceChunk> ChunkWithMetadata(string text, int maxChunkLength = DefaultMaxChunkLength) =>
        ChunkAligned(text, null, null, maxChunkLength);

    /// <summary>
    ///     Splits <paramref name="text"/> like <see cref="ChunkWithMetadata"/>, additionally
    ///     carrying <paramref name="annotations"/> into the chunks that contain them and, when
    ///     <paramref name="sentenceBreaks"/> is supplied, splitting sentences there instead of at
    ///     sentence-ending punctuation.
    /// </summary>
    /// <param name="text">The plain text to chunk. Must not be <see langword="null"/>.</param>
    /// <param name="annotations">
    ///     Annotations with offsets (UTF-16 code unit indices) relative to <paramref name="text"/>,
    ///     or <see langword="null"/> for none. Each chunk receives the annotations lying wholly
    ///     inside it, re-based to the chunk's own text, in input order. An annotation that crosses
    ///     a chunk boundary, starts or ends on whitespace that chunking trims, has an empty value and
    ///     lies on a dropped degenerate run, or lies outside the text is dropped rather than
    ///     misplaced. A non-empty-valued annotation wholly inside a punctuation-only piece keeps
    ///     that piece as its own chunk.
    /// </param>
    /// <param name="sentenceBreaks">
    ///     <see langword="null"/> to use the sentence-punctuation heuristic; otherwise the
    ///     authoritative sentence boundaries, where an entry <c>k</c> means a sentence ends
    ///     immediately before index <c>k</c> of <paramref name="text"/>. Out-of-range entries
    ///     (and <c>0</c>/the text length) are ignored. An empty list means no sentence splits;
    ///     clause splitting and the length budget still apply. Supplying it, or any annotations,
    ///     also makes the "word-less chunk" test Unicode-scalar aware, so a chunk whose only
    ///     letters are supplementary-plane characters is kept; with neither, the legacy
    ///     per-code-unit test applies.
    /// </param>
    /// <param name="maxChunkLength">
    ///     The length budget, in characters, past which a chunk is split further on whitespace.
    ///     Must be greater than zero. Defaults to <see cref="DefaultMaxChunkLength"/>.
    /// </param>
    /// <returns>
    ///     An ordered, read-only list of non-empty, trimmed chunks with ellipsis metadata and
    ///     re-based annotations. Empty when <paramref name="text"/> is empty or all whitespace.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="maxChunkLength"/> is less than or equal to zero.
    /// </exception>
    public static IReadOnlyList<SentenceChunk> ChunkAligned(
        string text,
        IReadOnlyList<SegmentAnnotation>? annotations,
        IReadOnlyList<int>? sentenceBreaks,
        int maxChunkLength = DefaultMaxChunkLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxChunkLength, 0);

        var whole = Trim(text, 0, text.Length);
        if (whole.Length == 0)
        {
            return [];
        }

        // Primary pass: sentence boundaries, from the analyzer when supplied, else punctuation.
        var primaryRanges = sentenceBreaks is null
            ? SplitOnBoundaries(text, whole, PrimaryBoundaries, mergeConsecutive: true)
            : SplitAtBreaks(text, whole, sentenceBreaks);

        // Secondary (clause) pass always applies, then the whitespace budget for long clauses.
        var pieces = new List<Piece>();
        foreach (var primaryRange in primaryRanges)
        {
            foreach (var secondaryRange in SplitOnBoundaries(text, primaryRange, SecondaryBoundaries, mergeConsecutive: false))
            {
                if (secondaryRange.Length <= maxChunkLength)
                {
                    pieces.Add(new Piece(
                        text[secondaryRange.Start..secondaryRange.End],
                        [new SourceRun(secondaryRange.Start, secondaryRange.Length, 0)]));
                    continue;
                }

                pieces.AddRange(SplitOnWhitespaceBudget(text, secondaryRange, maxChunkLength));
            }
        }

        // Text that came through an analysis (authoritative breaks and/or annotations) judges
        // "word content" per Unicode scalar, so a supplementary-plane letter is not mistaken for
        // punctuation; text with neither keeps the legacy per-UTF-16-code-unit predicate unchanged.
        var scalarAware = sentenceBreaks is not null || annotations is { Count: > 0 };
        var annotated = annotations is { Count: > 0 } ? FindAnnotatedPieces(text.Length, pieces, annotations) : null;
        var merged = MergeDegeneratePunctuationPieces(pieces, scalarAware, annotated);
        var rebased = annotations is { Count: > 0 } ? RebaseAnnotations(text.Length, merged, annotations) : null;

        var chunks = new SentenceChunk[merged.Count];
        for (var i = 0; i < chunks.Length; i++)
        {
            var chunk = new SentenceChunk(merged[i].Text, EndsWithEllipsis(merged[i].Text));
            chunks[i] = rebased?[i] is { Count: > 0 } pieceAnnotations
                ? chunk with { Annotations = Array.AsReadOnly(pieceAnnotations.ToArray()) }
                : chunk;
        }

        return chunks;
    }

    /// <summary>
    ///     Re-bases every annotation onto the piece that wholly contains it, using each piece's
    ///     source runs to translate original-text offsets into piece-text offsets.
    /// </summary>
    /// <param name="textLength">The length of the original source text.</param>
    /// <param name="pieces">The final, merged pieces, in source order.</param>
    /// <param name="annotations">The annotations, relative to the original source text.</param>
    /// <returns>
    ///     One list per piece (null when a piece received none), preserving input order. An
    ///     annotation not wholly covered by the runs of one piece is dropped.
    /// </returns>
    private static List<SegmentAnnotation>?[] RebaseAnnotations(
        int textLength,
        List<Piece> pieces,
        IReadOnlyList<SegmentAnnotation> annotations)
    {
        var runs = FlattenRuns(pieces);

        var result = new List<SegmentAnnotation>?[pieces.Count];
        foreach (var annotation in annotations)
        {
            if (!TryLocate(runs, annotation, textLength, out var first, out var last))
            {
                continue;
            }

            var startRun = runs[first].Run;
            var endRun = runs[last].Run;
            var newStart = startRun.TextStart + (annotation.Start - startRun.SourceStart);
            var newEnd = endRun.TextStart + (annotation.End - 1 - endRun.SourceStart) + 1;

            var pieceIndex = runs[first].PieceIndex;
            result[pieceIndex] ??= [];
            result[pieceIndex]!.Add(new SegmentAnnotation(newStart, newEnd - newStart, annotation.Kind, annotation.Value));
        }

        return result;
    }

    /// <summary>
    ///     Finds the pieces that wholly contain at least one annotation with a non-empty value,
    ///     using the same placement test as <see cref="RebaseAnnotations"/>. Such a piece carries
    ///     something to speak (for example a normalized symbol) and is therefore content even when
    ///     it has no letter or digit.
    /// </summary>
    /// <param name="textLength">The length of the original source text.</param>
    /// <param name="pieces">The pieces, in source order, before degenerate-piece merging.</param>
    /// <param name="annotations">The annotations, relative to the original source text.</param>
    /// <returns>
    ///     One flag per piece, or <see langword="null"/> when no piece qualifies. An annotation
    ///     with an empty value (it speaks nothing) or one crossing a piece boundary (it will be
    ///     dropped) does not qualify its piece.
    /// </returns>
    private static bool[]? FindAnnotatedPieces(
        int textLength,
        List<Piece> pieces,
        IReadOnlyList<SegmentAnnotation> annotations)
    {
        var runs = FlattenRuns(pieces);

        bool[]? result = null;
        foreach (var annotation in annotations)
        {
            if (annotation is null
                || annotation.Value.Length == 0
                || !TryLocate(runs, annotation, textLength, out var first, out _))
            {
                continue;
            }

            result ??= new bool[pieces.Count];
            result[runs[first].PieceIndex] = true;
        }

        return result;
    }

    /// <summary>
    ///     Flattens every run of every piece, in ascending source order, so a binary search finds
    ///     the run holding any given source index. A dropped degenerate piece contributes no
    ///     runs, so annotations on it find nothing.
    /// </summary>
    /// <param name="pieces">The pieces, in source order.</param>
    /// <returns>The runs, each tagged with the index of the piece that owns it.</returns>
    private static List<(int PieceIndex, SourceRun Run)> FlattenRuns(List<Piece> pieces)
    {
        var runs = new List<(int PieceIndex, SourceRun Run)>();
        for (var i = 0; i < pieces.Count; i++)
        {
            foreach (var run in pieces[i].Runs)
            {
                runs.Add((i, run));
            }
        }

        return runs;
    }

    /// <summary>
    ///     Locates the runs holding the first and last character of an annotation, succeeding
    ///     only when both exist and belong to the same piece.
    /// </summary>
    /// <param name="runs">The flattened runs.</param>
    /// <param name="annotation">The annotation to place; <see langword="null"/> never succeeds.</param>
    /// <param name="textLength">The length of the original source text.</param>
    /// <param name="first">Receives the index of the run holding the annotation's first character.</param>
    /// <param name="last">Receives the index of the run holding the annotation's last character.</param>
    /// <returns>
    ///     <see langword="true"/> when the annotation lies wholly inside one piece;
    ///     <see langword="false"/> when it is <see langword="null"/>, outside the text, or
    ///     not wholly inside one piece.
    /// </returns>
    private static bool TryLocate(
        List<(int PieceIndex, SourceRun Run)> runs,
        SegmentAnnotation? annotation,
        int textLength,
        out int first,
        out int last)
    {
        first = -1;
        last = -1;
        if (annotation is null || annotation.End > textLength)
        {
            return false;
        }

        first = FindRun(runs, annotation.Start);
        last = FindRun(runs, annotation.End - 1);
        return first >= 0 && last >= 0 && runs[first].PieceIndex == runs[last].PieceIndex;
    }

    /// <summary>
    ///     Finds the run whose source range contains <paramref name="sourceIndex"/>.
    /// </summary>
    /// <param name="runs">The runs, ascending by source start and non-overlapping.</param>
    /// <param name="sourceIndex">The index in the original source text.</param>
    /// <returns>The index into <paramref name="runs"/>, or -1 when no run covers the index.</returns>
    private static int FindRun(List<(int PieceIndex, SourceRun Run)> runs, int sourceIndex)
    {
        var low = 0;
        var high = runs.Count - 1;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var run = runs[mid].Run;
            if (sourceIndex < run.SourceStart)
            {
                high = mid - 1;
            }
            else if (sourceIndex >= run.SourceStart + run.Length)
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

    /// <summary>
    ///     Merges every piece whose trimmed text contains no letter or digit character at all
    ///     (just punctuation and/or whitespace, e.g. a lone comma or a whitespace-spaced run of
    ///     dots) onto the end of the immediately preceding non-empty piece, joined by a single
    ///     space; drops such a piece entirely when there is no preceding piece to merge into (a
    ///     degenerate run at the very start of the text). A piece flagged in
    ///     <paramref name="annotated"/> is content, so it is kept as its own piece.
    /// </summary>
    /// <param name="pieces">The ordered, already-split, non-empty pieces to merge.</param>
    /// <param name="scalarAware">
    ///     <see langword="true"/> to detect word content per Unicode scalar (analyzed text);
    ///     <see langword="false"/> for the legacy per-code-unit detection.
    /// </param>
    /// <param name="annotated">
    ///     One flag per piece marking a piece that wholly contains a non-empty-valued annotation,
    ///     or <see langword="null"/> (always the case without annotations) for none.
    /// </param>
    /// <returns>
    ///     An ordered list of pieces with no degenerate, word-less entries other than annotated ones.
    /// </returns>
    private static List<Piece> MergeDegeneratePunctuationPieces(
        IReadOnlyList<Piece> pieces,
        bool scalarAware,
        bool[]? annotated)
    {
        var merged = new List<Piece>();
        for (var index = 0; index < pieces.Count; index++)
        {
            var piece = pieces[index];
            if ((annotated is not null && annotated[index]) || HasWordContent(piece.Text, scalarAware))
            {
                merged.Add(piece);
                continue;
            }

            if (merged.Count > 0)
            {
                // The appended runs move past the previous text plus the joining space.
                var previous = merged[^1];
                var offset = previous.Text.Length + 1;
                var runs = new SourceRun[previous.Runs.Length + piece.Runs.Length];
                previous.Runs.CopyTo(runs, 0);
                for (var i = 0; i < piece.Runs.Length; i++)
                {
                    runs[previous.Runs.Length + i] = piece.Runs[i] with { TextStart = piece.Runs[i].TextStart + offset };
                }

                merged[^1] = new Piece($"{previous.Text} {piece.Text}", runs);
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
    /// <param name="scalarAware">
    ///     <see langword="true"/> to test each Unicode scalar (so a surrogate pair that encodes a
    ///     supplementary-plane letter or digit counts); <see langword="false"/> to test each
    ///     UTF-16 code unit, the legacy behavior in which such a pair has no word content.
    /// </param>
    /// <returns><see langword="true"/> when at least one letter or digit is present.</returns>
    private static bool HasWordContent(string text, bool scalarAware)
    {
        if (!scalarAware)
        {
            return text.Any(char.IsLetterOrDigit);
        }

        // A lone surrogate enumerates as U+FFFD (not a letter), matching the legacy treatment.
        return text.EnumerateRunes().Any(Rune.IsLetterOrDigit);
    }

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
    ///     Narrows a range of <paramref name="source"/> to exclude leading and trailing
    ///     whitespace, using the same whitespace definition as <see cref="string.Trim()"/>.
    /// </summary>
    /// <param name="source">The original source text.</param>
    /// <param name="start">The inclusive start of the range.</param>
    /// <param name="end">The exclusive end of the range.</param>
    /// <returns>The trimmed range, with <c>Length == 0</c> when the range is all whitespace.</returns>
    private static SourceRange Trim(string source, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(source[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(source[end - 1]))
        {
            end--;
        }

        return new SourceRange(start, end);
    }

    /// <summary>
    ///     Adds the trimmed form of a range to <paramref name="ranges"/> when it is not empty.
    /// </summary>
    /// <param name="ranges">The list to add to.</param>
    /// <param name="source">The original source text.</param>
    /// <param name="start">The inclusive start of the range.</param>
    /// <param name="end">The exclusive end of the range.</param>
    private static void AddTrimmed(List<SourceRange> ranges, string source, int start, int end)
    {
        var trimmed = Trim(source, start, end);
        if (trimmed.Length > 0)
        {
            ranges.Add(trimmed);
        }
    }

    /// <summary>
    ///     Splits a range at authoritative analyzer sentence breaks, ignoring any break not
    ///     strictly inside the range.
    /// </summary>
    /// <param name="source">The original source text.</param>
    /// <param name="range">The trimmed range to split.</param>
    /// <param name="breaks">The analyzer's break offsets, relative to <paramref name="source"/>.</param>
    /// <returns>An ordered list of non-empty, trimmed ranges.</returns>
    private static List<SourceRange> SplitAtBreaks(string source, SourceRange range, IReadOnlyList<int> breaks)
    {
        var cuts = new SortedSet<int>();
        foreach (var offset in breaks)
        {
            if (offset > range.Start && offset < range.End)
            {
                cuts.Add(offset);
            }
        }

        var ranges = new List<SourceRange>();
        var start = range.Start;
        foreach (var cut in cuts)
        {
            AddTrimmed(ranges, source, start, cut);
            start = cut;
        }

        AddTrimmed(ranges, source, start, range.End);
        return ranges;
    }

    /// <summary>
    ///     Splits a range immediately after each occurrence of any boundary character, keeping
    ///     the boundary attached to the piece that precedes it.
    /// </summary>
    /// <param name="source">The original source text.</param>
    /// <param name="range">The trimmed range to split.</param>
    /// <param name="boundaries">The characters to split after.</param>
    /// <param name="mergeConsecutive">
    ///     When <see langword="true"/>, a maximal run of consecutive boundary characters (in any
    ///     combination drawn from <paramref name="boundaries"/>, e.g. an ellipsis <c>...</c> or a
    ///     mixed terminator such as <c>?!</c>) is treated as a single boundary: the split happens
    ///     only once, after the last character of the run. When <see langword="false"/>, every
    ///     occurrence of a boundary character splits individually, even when immediately
    ///     adjacent to another boundary character.
    /// </param>
    /// <returns>An ordered list of non-empty, trimmed ranges.</returns>
    private static List<SourceRange> SplitOnBoundaries(
        string source,
        SourceRange range,
        char[] boundaries,
        bool mergeConsecutive)
    {
        var ranges = new List<SourceRange>();
        var start = range.Start;
        var i = range.Start;
        while (i < range.End)
        {
            if (Array.IndexOf(boundaries, source[i]) < 0)
            {
                i++;
                continue;
            }

            // A boundary character embedded in a numeral - either flanked by digits on both sides
            // (e.g. the ":" in "12:30" or the "," in "1,000"), or a decimal point followed by a
            // digit with no digit (and no letter) before it (e.g. the leading "." in ".5") - is
            // part of a numeral, not a clause/sentence boundary; leave it attached rather than
            // splitting. See IsDigitFlanked for the exact discriminator.
            if (IsDigitFlanked(source, i, range))
            {
                i++;
                continue;
            }

            // Extend over a maximal run of consecutive boundary characters when requested, so
            // e.g. an ellipsis ("...") or a mixed terminator ("?!") splits once, not repeatedly.
            var last = i;
            if (mergeConsecutive)
            {
                while (last + 1 < range.End && Array.IndexOf(boundaries, source[last + 1]) >= 0)
                {
                    last++;
                }
            }

            AddTrimmed(ranges, source, start, last + 1);
            start = last + 1;
            i = last + 1;
        }

        AddTrimmed(ranges, source, start, range.End);
        return ranges;
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
    ///     "point five"). Neighbors are looked up only inside <paramref name="range"/>, never
    ///     beyond it, so a split piece behaves exactly as if it were scanned on its own.
    /// </summary>
    /// <param name="source">The original source text being scanned.</param>
    /// <param name="index">The index of the boundary character to check.</param>
    /// <param name="range">The range being scanned; neighbors outside it are ignored.</param>
    /// <returns><see langword="true"/> when the character is part of a numeral.</returns>
    private static bool IsDigitFlanked(string source, int index, SourceRange range)
    {
        if (index + 1 >= range.End || !char.IsDigit(source[index + 1]))
        {
            return false;
        }

        if (index > range.Start && char.IsDigit(source[index - 1]))
        {
            return true;
        }

        return source[index] == '.' && (index == range.Start || !char.IsLetter(source[index - 1]));
    }

    /// <summary>
    ///     Splits a range on whitespace boundaries so that no returned piece exceeds
    ///     <paramref name="maxChunkLength"/>, except a single word that alone exceeds the budget
    ///     (which is returned whole rather than broken mid-word). Words are re-joined with a
    ///     single space, so each piece records one source run per word.
    /// </summary>
    /// <remarks>
    ///     A piece takes as many words as fit the budget, and then ends at the best phrase boundary
    ///     among them (see <see cref="ChooseBoundary"/>) instead of wherever the budget happens to run
    ///     out, so a long clause is not cut between a function word and the word it belongs to
    ///     (<i>the last full measure of | devotion</i>).
    /// </remarks>
    /// <param name="source">The original source text.</param>
    /// <param name="range">The range to split, still longer than the budget as a whole.</param>
    /// <param name="maxChunkLength">The length budget, in characters.</param>
    /// <returns>An ordered list of non-empty pieces.</returns>
    private static List<Piece> SplitOnWhitespaceBudget(string source, SourceRange range, int maxChunkLength)
    {
        // The words of the range
        var words = new List<SourceRange>();
        var i = range.Start;
        while (i < range.End)
        {
            if (char.IsWhiteSpace(source[i]))
            {
                i++;
                continue;
            }

            var wordStart = i;
            while (i < range.End && !char.IsWhiteSpace(source[i]))
            {
                i++;
            }

            words.Add(new SourceRange(wordStart, i));
        }

        // Each piece takes the words that fit, then ends at the best boundary among them
        var pieces = new List<Piece>();
        var start = 0;
        while (start < words.Count)
        {
            var fit = start + 1;
            var length = words[start].Length;
            while (fit < words.Count && length + 1 + words[fit].Length <= maxChunkLength)
            {
                length += 1 + words[fit].Length;
                fit++;
            }

            var end = fit == words.Count ? fit : ChooseBoundary(source, words, start, fit, maxChunkLength);
            pieces.Add(JoinWords(source, words.GetRange(start, end - start)));
            start = end;
        }

        return pieces;
    }

    /// <summary>
    ///     Chooses where a piece ends among the words that fit the budget: at a dash or a bracket, else before a
    ///     conjunction, else after any word that is not a function word; the end of the last word that fits only
    ///     when none of these leaves a reasonably full piece.
    /// </summary>
    /// <remarks>
    ///     A dash or a bracket is where a writer pauses, so a piece may end there when it holds at least a quarter of
    ///     the budget. A piece may end before <i>and</i>, <i>but</i>, <i>that</i>, <i>which</i>, <i>because</i> and
    ///     the like (a clause starts there) when it holds at least two fifths, and after any other word that is not a
    ///     function word (an article, preposition, auxiliary, pronoun or conjunction, each of which needs the word
    ///     after it) when it holds at least half. Within one rule the latest boundary wins. A boundary is never put
    ///     before a mark that carries no word, which would start the next piece with a lone dash.
    /// </remarks>
    /// <param name="source">The original source text.</param>
    /// <param name="words">The words of the range being split.</param>
    /// <param name="start">The index of the first word of the piece.</param>
    /// <param name="fit">The index after the last word that fits the budget; always less than the number of words.</param>
    /// <param name="maxChunkLength">The length budget, in characters.</param>
    /// <returns>The index after the last word of the piece, greater than <paramref name="start"/> and at most <paramref name="fit"/>.</returns>
    private static int ChooseBoundary(string source, List<SourceRange> words, int start, int fit, int maxChunkLength)
    {
        var dash = -1;
        var conjunction = -1;
        var phrase = -1;
        var length = 0;
        for (var end = start + 1; end <= fit; end++)
        {
            length += words[end - 1].Length + (end > start + 1 ? 1 : 0);
            var last = source.AsSpan(words[end - 1].Start, words[end - 1].Length);
            var next = source.AsSpan(words[end].Start, words[end].Length);
            if (!HasWordCharacter(next))
            {
                // The next word is a bare mark (a dash): it stays with the word before it
                continue;
            }

            if (length * 4 >= maxChunkLength && (EndsWithDash(last) || next[0] is '(' or '['))
            {
                dash = end;
            }

            var lastWord = TrimToWord(last);
            if (length * 5 >= maxChunkLength * 2 && !IsFunctionWord(lastWord) && IsConjunction(TrimToWord(next)))
            {
                conjunction = end;
            }

            if (length * 2 >= maxChunkLength && !IsFunctionWord(lastWord))
            {
                phrase = end;
            }
        }

        return new[] { dash, conjunction, phrase }.FirstOrDefault(boundary => boundary > 0, fit);
    }

    /// <summary>
    ///     Tests whether a word is a dash or ends in one (<c>--</c>, <c>-</c>, an en dash or an em dash).
    /// </summary>
    /// <param name="word">The word as written.</param>
    /// <returns><see langword="true"/> for a dash, or a word directly followed by a dash.</returns>
    private static bool EndsWithDash(ReadOnlySpan<char> word) =>
        word.Length > 0 && (word[^1] is '\u2014' or '\u2013' || word.EndsWith("--", StringComparison.Ordinal) || (word.Length == 1 && word[0] == '-'));

    /// <summary>
    ///     Tests whether a word has a letter or a digit.
    /// </summary>
    /// <param name="word">The word as written.</param>
    /// <returns><see langword="true"/> when the word is more than a mark.</returns>
    private static bool HasWordCharacter(ReadOnlySpan<char> word)
    {
        for (var i = 0; i < word.Length; i++)
        {
            if (char.IsLetterOrDigit(word[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Removes the marks around a word and lower-cases it.
    /// </summary>
    /// <param name="word">The word as written.</param>
    /// <returns>The letters, digits and inner apostrophes of the word in lower case.</returns>
    private static string TrimToWord(ReadOnlySpan<char> word)
    {
        var start = 0;
        var end = word.Length;
        while (start < end && !char.IsLetterOrDigit(word[start]))
        {
            start++;
        }

        while (end > start && !char.IsLetterOrDigit(word[end - 1]))
        {
            end--;
        }

        return word[start..end].ToString().ToLowerInvariant();
    }

    /// <summary>
    ///     Tests whether an English word needs the word after it: an article, preposition, auxiliary, pronoun or conjunction.
    /// </summary>
    /// <param name="word">The word in lower case, without marks.</param>
    /// <returns><see langword="true"/> for such a word; a piece is not ended after one when it can be helped.</returns>
    private static bool IsFunctionWord(string word) => FunctionWords.Contains(word) || ClauseStarters.Contains(word);

    /// <summary>
    ///     Tests whether an English word starts a clause (a coordinating or subordinating conjunction or a relative pronoun).
    /// </summary>
    /// <param name="word">The word in lower case, without marks.</param>
    /// <returns><see langword="true"/> when a piece may end before the word.</returns>
    private static bool IsConjunction(string word) => ClauseStarters.Contains(word);

    /// <summary>
    ///     Joins words with a single space, recording one source run per word.
    /// </summary>
    /// <param name="source">The original source text.</param>
    /// <param name="words">The ordered word ranges to join.</param>
    /// <returns>The joined piece.</returns>
    private static Piece JoinWords(string source, List<SourceRange> words)
    {
        var builder = new System.Text.StringBuilder();
        var runs = new SourceRun[words.Count];
        for (var i = 0; i < words.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            runs[i] = new SourceRun(words[i].Start, words[i].Length, builder.Length);
            builder.Append(source, words[i].Start, words[i].Length);
        }

        return new Piece(builder.ToString(), runs);
    }
}

/// <summary>
///     One chunk produced by <see cref="SentenceChunker.ChunkWithMetadata"/> or
///     <see cref="SentenceChunker.ChunkAligned"/>: its text, whether it ends in a genuine
///     ellipsis so a caller can render a longer pause there, and any text-analysis annotations
///     re-based onto the chunk's text.
/// </summary>
/// <param name="Text">The chunk's non-empty, trimmed text.</param>
/// <param name="EndsWithEllipsis">
///     <see langword="true"/> when <paramref name="Text"/> ends in a genuine ellipsis - three or
///     more consecutive <c>.</c> characters, with or without interspersed whitespace (e.g.
///     <c>"..."</c> or <c>". . ."</c>) - which should render a longer pause than an ordinary
///     sentence-ending <c>.</c>/<c>!</c>/<c>?</c>.
/// </param>
/// <remarks>
///     <see cref="Annotations"/> is not a positional member so the two-argument form stays
///     source- and binary-compatible. The type is immutable and safe to share across threads.
/// </remarks>
public readonly record struct SentenceChunk(string Text, bool EndsWithEllipsis)
{
    /// <summary>
    ///     Backing field for <see cref="Annotations"/>; null (rather than the shared empty list)
    ///     when there are none, so a default struct and an explicitly empty one stay equal.
    /// </summary>
    private readonly IReadOnlyList<SegmentAnnotation>? _annotations;

    /// <summary>
    ///     Gets the annotations lying wholly inside this chunk, with offsets relative to
    ///     <see cref="Text"/>; empty when there are none, including for a default struct.
    /// </summary>
    /// <remarks>
    ///     The assigned list is defensively copied into a read-only collection, so later changes
    ///     to the caller's list never affect the chunk. As with <see cref="SpeechSegment"/>, the
    ///     compiler-generated equality compares the stored list by reference, so two chunks are
    ///     equal only when they share the same copy (for example, one derived from the other
    ///     with <see langword="with"/>) or both have none.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
    /// <exception cref="ArgumentException">Thrown when the list contains null.</exception>
    public IReadOnlyList<SegmentAnnotation> Annotations
    {
        get => _annotations ?? AnnotationList.Empty;
        init
        {
            var copy = AnnotationList.Copy(value, nameof(Annotations));
            _annotations = copy.Count == 0 ? null : copy;
        }
    }
}
