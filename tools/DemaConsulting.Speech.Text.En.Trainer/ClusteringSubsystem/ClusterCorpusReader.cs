using System.Runtime.ExceptionServices;
using System.Text;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

/// <summary>One paragraph of a tokenized book: its text and the token spans of each of its sentences.</summary>
/// <param name="Text">The paragraph text: the paragraph's lines joined by single spaces.</param>
/// <param name="Sentences">Per sentence, the token spans as flat <c>start, length</c> pairs indexing <paramref name="Text"/>.</param>
internal sealed record CorpusParagraph(string Text, IReadOnlyList<int[]> Sentences)
{
    /// <summary>
    ///     Gets the text of a sentence: from its first token's start to its last token's end.
    /// </summary>
    /// <param name="sentence">The 0-based sentence index.</param>
    /// <returns>The sentence text.</returns>
    public string SentenceText(int sentence)
    {
        var spans = Sentences[sentence];
        var start = spans[0];
        var end = spans[^2] + spans[^1];
        return Text[start..end];
    }
}

/// <summary>One tokenized book of the corpus.</summary>
/// <param name="SourceId">The source id.</param>
/// <param name="Paragraphs">The paragraphs in body order.</param>
internal sealed record CorpusBook(string SourceId, IReadOnlyList<CorpusParagraph> Paragraphs);

/// <summary>A sentence that must not be counted: its location and the SHA-256 of its text, which must still match.</summary>
/// <param name="SourceId">The source id.</param>
/// <param name="Paragraph">The 0-based paragraph index.</param>
/// <param name="Sentence">The 0-based sentence index within the paragraph.</param>
/// <param name="TextSha256">The lowercase hexadecimal SHA-256 of the sentence's UTF-8 text.</param>
internal sealed record SkippedSentence(string SourceId, int Paragraph, int Sentence, string TextSha256);

/// <summary>The reader's settings.</summary>
/// <param name="MinCount">The smallest corpus count of a vocabulary word; at least 1.</param>
/// <param name="MaxWords">The largest vocabulary, or 0 for no limit.</param>
internal sealed record ReaderSettings(int MinCount, int MaxWords);

/// <summary>The integer statistics of the corpus and its vocabulary, ready for clustering.</summary>
/// <param name="Words">The vocabulary words (normalized forms) by rank: count descending, then ordinal.</param>
/// <param name="Counts">The corpus count of each vocabulary word, by rank.</param>
/// <param name="Bigrams">The within-sentence bigram counts between adjacent vocabulary words, by rank.</param>
/// <param name="FormCounts">The count of every eligible normalized form, including those below the minimum count.</param>
/// <param name="Paragraphs">The number of paragraphs read.</param>
/// <param name="Sentences">The number of sentences counted (removed sentences excluded).</param>
/// <param name="Tokens">The number of tokens counted (removed sentences excluded).</param>
/// <param name="RemovedSentences">The number of sentences skipped as removals.</param>
/// <param name="RemovedTokens">The number of tokens in the skipped sentences.</param>
internal sealed record CorpusCounts(
    IReadOnlyList<string> Words,
    IReadOnlyList<long> Counts,
    BigramGraph Bigrams,
    IReadOnlyDictionary<string, long> FormCounts,
    long Paragraphs,
    long Sentences,
    long Tokens,
    long RemovedSentences,
    long RemovedTokens)
{
    /// <summary>Gets the number of distinct eligible forms in the counted text.</summary>
    public int Types => FormCounts.Count;

    /// <summary>
    ///     Gets the share of counted tokens covered by the first words of the vocabulary, in units of one ten-thousandth.
    /// </summary>
    /// <param name="words">The number of vocabulary words; clamped to the vocabulary size.</param>
    /// <returns>The coverage, rounded down, 0 to 10,000.</returns>
    public int CoveragePerTenThousand(int words)
    {
        long covered = 0;
        for (var rank = 0; rank < Math.Min(words, Counts.Count); rank++)
        {
            covered += Counts[rank];
        }

        return Tokens == 0 ? 0 : (int)(covered * 10_000 / Tokens);
    }
}

/// <summary>The tokenized corpus: every used book split into paragraphs, sentences and tokens.</summary>
/// <param name="Books">The books in ordinal source-id order.</param>
internal sealed record TokenizedCorpus(IReadOnlyList<CorpusBook> Books)
{
    /// <summary>Gets the total number of tokens.</summary>
    public long Tokens => Books.Sum(static book => book.Paragraphs.Sum(static paragraph => paragraph.Sentences.Sum(static sentence => (long)sentence.Length / 2)));
}

/// <summary>
///     Reads the normalized corpus into the integer word and bigram counts the clusterer needs, tokenizing exactly as the
///     runtime analyzer does so every table word hashes to the runtime's lower-case key.
/// </summary>
/// <remarks>
///     <para>
///     Each body is split into paragraphs on blank lines (lines joined by single spaces), each paragraph is split into
///     sentences and tokens by the analyzer's own model-free tokenizer, and each token is normalized with the runtime's
///     rule (curly single quotes become the apostrophe, ASCII capitals become lower case, a token with a digit and no
///     letter becomes <c>&lt;num&gt;</c>). Once per distinct token text the reader asserts that the hash of its normalized
///     form equals the runtime key <see cref="ContextToken.LowerHash"/>, so the table can never be keyed differently from
///     the lookup. A form is eligible when it has at most 40 characters, all in U+0021 to U+007E, U+00A1 to U+00FF or
///     U+2010 to U+2027; other tokens break adjacency like a sentence boundary.
///     </para>
///     <para>
///     Counting is deterministic for any thread count: books are tokenized in parallel into separate slots, merged in
///     ordinal source-id order, the vocabulary is ordered by count descending then ordinal form, and bigrams are sorted
///     as integers; no hash-table iteration order is ever observable. Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class ClusterCorpusReader
{
    /// <summary>The version of the reading rules; recorded so a rule change invalidates the table.</summary>
    public const int ReaderVersion = 1;

    /// <summary>The longest eligible normalized form, in UTF-16 code units.</summary>
    public const int MaximumFormLength = 40;

    /// <summary>The normalized form of a token with a digit and no letter (the runtime's rule).</summary>
    public const string NumberForm = "<num>";

    /// <summary>
    ///     Tokenizes the bodies of the corpus.
    /// </summary>
    /// <param name="bodies">The verified bodies in ordinal source-id order.</param>
    /// <param name="threads">The number of worker threads; at least 1. The result does not depend on it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tokenized corpus in the order of <paramref name="bodies"/>.</returns>
    public static TokenizedCorpus Tokenize(IReadOnlyList<(string SourceId, string Text)> bodies, int threads, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentOutOfRangeException.ThrowIfLessThan(threads, 1);
        var books = new CorpusBook[bodies.Count];
        ForEachIndex(bodies.Count, threads, cancellationToken, index => books[index] = TokenizeBook(bodies[index].SourceId, bodies[index].Text, cancellationToken));
        return new TokenizedCorpus(books);
    }

    /// <summary>
    ///     Splits a body into paragraphs.
    /// </summary>
    /// <param name="body">The normalized body (LF line endings).</param>
    /// <returns>The paragraph texts in body order, each with its lines joined by single spaces.</returns>
    public static IReadOnlyList<string> Paragraphs(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var result = new List<string>();
        var lines = new List<string>();
        foreach (var line in body.Split('\n'))
        {
            if (line.Length == 0)
            {
                if (lines.Count > 0)
                {
                    result.Add(string.Join(' ', lines));
                    lines.Clear();
                }

                continue;
            }

            lines.Add(line);
        }

        if (lines.Count > 0)
        {
            result.Add(string.Join(' ', lines));
        }

        return result;
    }

    /// <summary>
    ///     Normalizes one token with the runtime's rule and decides whether it may enter the vocabulary.
    /// </summary>
    /// <param name="token">The token text.</param>
    /// <returns>The normalized form, or null when the form is not eligible.</returns>
    public static string? NormalizeEligible(ReadOnlySpan<char> token)
    {
        var form = Normalize(token);
        return form.Length <= MaximumFormLength && form.All(IsEligible) ? form : null;
    }

    /// <summary>
    ///     Normalizes one token with the runtime's rule.
    /// </summary>
    /// <param name="token">The token text.</param>
    /// <returns><c>&lt;num&gt;</c> for a token with a digit and no letter; otherwise the token with curly single quotes as apostrophes and ASCII capitals lower-cased.</returns>
    public static string Normalize(ReadOnlySpan<char> token)
    {
        var hasLetter = false;
        var hasDigit = false;
        foreach (var rune in token.EnumerateRunes())
        {
            hasLetter |= Rune.IsLetter(rune);
            hasDigit |= Rune.IsDigit(rune);
        }

        if (hasDigit && !hasLetter)
        {
            return NumberForm;
        }

        return string.Create(token.Length, token, static (destination, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var unit = source[i];
                destination[i] = unit is '\u2018' or '\u2019' ? '\'' : FeatureHashing.FoldAscii(unit);
            }
        });
    }

    /// <summary>
    ///     Determines whether a character may appear in a table word.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for U+0021 to U+007E, U+00A1 to U+00FF and U+2010 to U+2027.</returns>
    public static bool IsEligible(char c) => c is (>= '\u0021' and <= '\u007e') or (>= '\u00a1' and <= '\u00ff') or (>= '\u2010' and <= '\u2027');

    /// <summary>
    ///     Counts words and bigrams, skipping the removed sentences.
    /// </summary>
    /// <param name="corpus">The tokenized corpus.</param>
    /// <param name="skipped">The sentences to skip; each must exist and its text must still match its SHA-256.</param>
    /// <param name="settings">The vocabulary settings.</param>
    /// <param name="threads">The number of worker threads; at least 1. The result does not depend on it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The counts.</returns>
    /// <exception cref="InvalidDataException">A skipped sentence does not exist or its text changed (the removals are stale).</exception>
    /// <exception cref="InvalidOperationException">A normalized form does not hash to the runtime key (the self-check failed).</exception>
    public static CorpusCounts Count(TokenizedCorpus corpus, IReadOnlyList<SkippedSentence> skipped, ReaderSettings settings, int threads, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(skipped);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.MinCount, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(settings.MaxWords);
        ArgumentOutOfRangeException.ThrowIfLessThan(threads, 1);

        // Phase 1 (parallel per book): local forms in first-appearance order and the token stream of local ids
        var skippedByBook = skipped.GroupBy(static removal => removal.SourceId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToList(), StringComparer.Ordinal);
        var unknown = skippedByBook.Keys.FirstOrDefault(id => !corpus.Books.Any(book => string.Equals(book.SourceId, id, StringComparison.Ordinal)));
        if (unknown is not null)
        {
            throw new InvalidDataException($"a removal names {unknown}, which is not a book of the corpus; the removals are stale");
        }

        var locals = new BookStream[corpus.Books.Count];
        ForEachIndex(
            corpus.Books.Count,
            threads,
            cancellationToken,
            index => locals[index] = StreamBook(
                corpus.Books[index],
                skippedByBook.TryGetValue(corpus.Books[index].SourceId, out var list) ? list : []));

        // Phase 2 (sequential, book order): global ids by first appearance, then integer word counts
        var globalIds = new Dictionary<string, int>(StringComparer.Ordinal);
        var forms = new List<string>();
        var maps = new int[locals.Length][];
        foreach (var (local, index) in locals.Select(static (local, index) => (local, index)))
        {
            var map = new int[local.Forms.Count];
            for (var i = 0; i < map.Length; i++)
            {
                if (!globalIds.TryGetValue(local.Forms[i], out var id))
                {
                    id = forms.Count;
                    globalIds.Add(local.Forms[i], id);
                    forms.Add(local.Forms[i]);
                }

                map[i] = id;
            }

            maps[index] = map;
        }

        var wordCounts = new long[forms.Count];
        for (var book = 0; book < locals.Length; book++)
        {
            var stream = locals[book].Stream;
            for (var i = 0; i < stream.Count; i++)
            {
                if (stream[i] >= 0)
                {
                    wordCounts[maps[book][stream[i]]]++;
                }
            }
        }

        // The vocabulary: count descending, then ordinal form, cut to the limit
        var order = Enumerable.Range(0, forms.Count)
            .Where(id => wordCounts[id] >= settings.MinCount)
            .OrderByDescending(id => wordCounts[id])
            .ThenBy(id => forms[id], StringComparer.Ordinal)
            .ToList();
        if (settings.MaxWords > 0 && order.Count > settings.MaxWords)
        {
            order.RemoveRange(settings.MaxWords, order.Count - settings.MaxWords);
        }

        var rankOf = new int[forms.Count];
        Array.Fill(rankOf, -1);
        for (var rank = 0; rank < order.Count; rank++)
        {
            rankOf[order[rank]] = rank;
        }

        var bigrams = CountBigrams(locals, maps, rankOf, order.Count);
        var formCounts = new Dictionary<string, long>(StringComparer.Ordinal);
        for (var id = 0; id < forms.Count; id++)
        {
            formCounts.Add(forms[id], wordCounts[id]);
        }

        return new CorpusCounts(
            [.. order.Select(id => forms[id])],
            [.. order.Select(id => wordCounts[id])],
            bigrams,
            formCounts,
            locals.Sum(static local => (long)local.ParagraphCount),
            locals.Sum(static local => local.SentenceCount),
            locals.Sum(static local => local.Tokens),
            locals.Sum(static local => local.RemovedSentences),
            locals.Sum(static local => local.RemovedTokens));
    }

    /// <summary>
    ///     Runs a loop body for every index on up to the given number of worker threads.
    /// </summary>
    /// <remarks>
    ///     Every body writes only its own slot, so the result does not depend on the thread count. A failure inside the
    ///     loop is rethrown as itself rather than wrapped, so callers see the documented exception types.
    /// </remarks>
    /// <param name="count">The number of indexes.</param>
    /// <param name="threads">The number of worker threads; at least 1.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="body">The body.</param>
    internal static void ForEachIndex(int count, int threads, CancellationToken cancellationToken, Action<int> body)
    {
        try
        {
            Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = threads, CancellationToken = cancellationToken }, body);
        }
        catch (AggregateException exception) when (exception.InnerExceptions.Count > 0)
        {
            ExceptionDispatchInfo.Capture(exception.InnerExceptions[0]).Throw();
        }
    }

    /// <summary>
    ///     Tokenizes one book.
    /// </summary>
    /// <param name="sourceId">The source id.</param>
    /// <param name="body">The body.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tokenized book.</returns>
    private static CorpusBook TokenizeBook(string sourceId, string body, CancellationToken cancellationToken)
    {
        var paragraphs = new List<CorpusParagraph>();
        foreach (var text in Paragraphs(body))
        {
            var sentences = PartOfSpeechAdapter.Shared.Analyze(text, false, cancellationToken);
            var spans = new List<int[]>(sentences.Count);
            foreach (var sentence in sentences)
            {
                var flat = new int[sentence.Count * 2];
                for (var i = 0; i < sentence.Count; i++)
                {
                    flat[2 * i] = sentence[i].Start;
                    flat[(2 * i) + 1] = sentence[i].Length;
                }

                spans.Add(flat);
            }

            paragraphs.Add(new CorpusParagraph(text, spans));
        }

        return new CorpusBook(sourceId, paragraphs);
    }

    /// <summary>
    ///     Turns one book into local form ids, skipping removed sentences and self-checking every distinct token text.
    /// </summary>
    /// <param name="book">The tokenized book.</param>
    /// <param name="skipped">The book's skipped sentences.</param>
    /// <returns>The book's local stream.</returns>
    private static BookStream StreamBook(CorpusBook book, List<SkippedSentence> skipped)
    {
        // Every skipped sentence must exist with the recorded text; otherwise the removals no longer fit the corpus
        var skip = new HashSet<(int, int)>();
        foreach (var removal in skipped)
        {
            if (removal.Paragraph < 0 || removal.Paragraph >= book.Paragraphs.Count
                || removal.Sentence < 0 || removal.Sentence >= book.Paragraphs[removal.Paragraph].Sentences.Count)
            {
                throw new InvalidDataException($"the removal {book.SourceId} {removal.Paragraph}/{removal.Sentence} names no sentence; the removals are stale");
            }

            var text = book.Paragraphs[removal.Paragraph].SentenceText(removal.Sentence);
            if (!string.Equals(GutenbergBody.Sha256Hex(Encoding.UTF8.GetBytes(text)), removal.TextSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"the removal {book.SourceId} {removal.Paragraph}/{removal.Sentence} no longer matches its text; the removals are stale");
            }

            skip.Add((removal.Paragraph, removal.Sentence));
        }

        var result = new BookStream();
        var localIds = new Dictionary<string, int>(StringComparer.Ordinal);
        var checkedTokens = new HashSet<string>(StringComparer.Ordinal);
        for (var p = 0; p < book.Paragraphs.Count; p++)
        {
            var paragraph = book.Paragraphs[p];
            result.ParagraphCount++;
            for (var s = 0; s < paragraph.Sentences.Count; s++)
            {
                var spans = paragraph.Sentences[s];
                if (skip.Contains((p, s)))
                {
                    result.RemovedSentences++;
                    result.RemovedTokens += spans.Length / 2;
                    continue;
                }

                result.SentenceCount++;
                for (var i = 0; i < spans.Length; i += 2)
                {
                    result.Tokens++;
                    var raw = paragraph.Text.AsSpan(spans[i], spans[i + 1]);
                    var form = NormalizeEligible(raw);
                    if (form is not null && checkedTokens.Add(raw.ToString()))
                    {
                        SelfCheck(paragraph.Text, spans[i], spans[i + 1], form);
                    }

                    if (form is null)
                    {
                        result.Stream.Add(-1);
                        continue;
                    }

                    if (!localIds.TryGetValue(form, out var id))
                    {
                        id = result.Forms.Count;
                        localIds.Add(form, id);
                        result.Forms.Add(form);
                    }

                    result.Stream.Add(id);
                }

                // A sentence boundary breaks adjacency
                result.Stream.Add(-1);
            }
        }

        return result;
    }

    /// <summary>
    ///     Asserts that a normalized form hashes to the runtime's lower-case key of its token.
    /// </summary>
    /// <param name="text">The paragraph text.</param>
    /// <param name="start">The token start.</param>
    /// <param name="length">The token length.</param>
    /// <param name="form">The normalized form.</param>
    /// <exception cref="InvalidOperationException">The hashes differ.</exception>
    private static void SelfCheck(string text, int start, int length, string form)
    {
        var runtime = HeteronymContext.Create(text, start, length, null, 0).LowerHash;
        if (FeatureHashing.Fnv1a(form) != runtime)
        {
            throw new InvalidOperationException($"the normalized form '{form}' does not hash to the runtime key of its token; the reader no longer matches the runtime");
        }
    }

    /// <summary>
    ///     Counts the bigrams of adjacent vocabulary tokens and builds the graph.
    /// </summary>
    /// <param name="locals">The book streams.</param>
    /// <param name="maps">The local-to-global id map of each book.</param>
    /// <param name="rankOf">The vocabulary rank of each global id, or -1.</param>
    /// <param name="words">The vocabulary size.</param>
    /// <returns>The bigram graph.</returns>
    private static BigramGraph CountBigrams(BookStream[] locals, int[][] maps, int[] rankOf, int words)
    {
        // Every adjacent pair of vocabulary tokens becomes one integer key (left rank, right rank)
        var keys = new List<long>((int)Math.Min(int.MaxValue / 2, locals.Sum(static local => (long)local.Stream.Count)));
        for (var book = 0; book < locals.Length; book++)
        {
            var previous = -1;
            foreach (var token in locals[book].Stream)
            {
                var rank = token < 0 ? -1 : rankOf[maps[book][token]];
                if (previous >= 0 && rank >= 0)
                {
                    keys.Add(((long)previous << 32) | (uint)rank);
                }

                previous = rank;
            }
        }

        // Sorting integers gives a fixed order; equal neighbors are then run-length counted
        var sorted = keys.ToArray();
        keys.Clear();
        Array.Sort(sorted);
        var unique = new List<long>();
        var counts = new List<long>();
        for (var i = 0; i < sorted.Length; i++)
        {
            if (unique.Count > 0 && unique[^1] == sorted[i])
            {
                counts[^1]++;
            }
            else
            {
                unique.Add(sorted[i]);
                counts.Add(1);
            }
        }

        return BigramGraph.FromSortedKeys(words, [.. unique], [.. counts]);
    }

    /// <summary>The local result of streaming one book.</summary>
    private sealed class BookStream
    {
        /// <summary>Gets the book's distinct eligible forms in first-appearance order.</summary>
        public List<string> Forms { get; } = [];

        /// <summary>Gets the stream of local form ids; -1 marks an ineligible token or a sentence boundary.</summary>
        public List<int> Stream { get; } = [];

        /// <summary>Gets or sets the number of paragraphs.</summary>
        public int ParagraphCount { get; set; }

        /// <summary>Gets or sets the number of counted sentences.</summary>
        public long SentenceCount { get; set; }

        /// <summary>Gets or sets the number of counted tokens.</summary>
        public long Tokens { get; set; }

        /// <summary>Gets or sets the number of skipped sentences.</summary>
        public long RemovedSentences { get; set; }

        /// <summary>Gets or sets the number of tokens in skipped sentences.</summary>
        public long RemovedTokens { get; set; }
    }
}
