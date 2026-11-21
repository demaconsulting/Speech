using System.Globalization;
using System.Text;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

/// <summary>One evaluation sentence the corpus must not repeat.</summary>
/// <param name="File">The evaluation file name, for example <c>dev.tsv</c>.</param>
/// <param name="Id">The row id.</param>
/// <param name="Sentence">The sentence text.</param>
internal sealed record EvaluationSentence(string File, string Id, string Sentence);

/// <summary>One corpus sentence removed by the leakage audit; a row of <c>cluster-removals.tsv</c>.</summary>
/// <param name="SourceId">The source id.</param>
/// <param name="Paragraph">The 0-based paragraph index in the body.</param>
/// <param name="Sentence">The 0-based sentence index in the paragraph.</param>
/// <param name="Rule">The first rule that matched: <c>exact</c>, <c>near</c> or <c>contains</c>.</param>
/// <param name="EvalFile">The evaluation file of the matched sentence (the first in file order).</param>
/// <param name="EvalId">The id of the matched evaluation sentence (the first by ordinal id in that file).</param>
/// <param name="TextSha256">The lowercase hexadecimal SHA-256 of the removed sentence's UTF-8 text.</param>
internal sealed record CorpusRemoval(string SourceId, int Paragraph, int Sentence, string Rule, string EvalFile, string EvalId, string TextSha256);

/// <summary>The audit counts of one evaluation file and rule.</summary>
/// <param name="EvalFile">The evaluation file.</param>
/// <param name="Rule">The rule.</param>
/// <param name="RemovedSentences">The corpus sentences removed and attributed to this file and rule.</param>
/// <param name="MatchedEvaluationSentences">The distinct evaluation sentences these removals name.</param>
/// <param name="BooksAffected">The distinct books these removals come from.</param>
internal sealed record AuditCount(string EvalFile, string Rule, int RemovedSentences, int MatchedEvaluationSentences, int BooksAffected);

/// <summary>
///     The content-level leakage audit of the word-cluster corpus: finds every corpus sentence that repeats, nearly
///     repeats or quotes an evaluation sentence, so it can be removed before counting.
/// </summary>
/// <remarks>
///     <para>
///     Book-level exclusion cannot catch an alternate edition, an anthology or a quotation of an evaluation sentence in
///     another book; this audit compares content. Every sentence of the development, natural holdout, independent holdout,
///     regression and generalization files is normalized with <see cref="SentenceKeys"/> (lower-case ASCII letters and
///     digits; words; word 5-grams), the same normalization and thresholds as the data check's leakage rules. The corpus
///     units are the reader's tokenizer sentences. The rules apply in order and a sentence is removed by the first that
///     matches: <b>exact</b> (equal keys); <b>near</b> (a corpus sentence of at least 8 words with at least 80 percent of
///     its distinct 5-grams among the evaluation 5-grams); <b>contains</b> (an evaluation sentence of at least 8 words
///     with at least 80 percent of its distinct 5-grams in one corpus paragraph, or of 5 to 7 words with all of them in one
///     corpus sentence, removes every sentence of that paragraph sharing a 5-gram with it).
///     </para>
///     <para>
///     The result is deterministic for any thread count: books are audited in parallel into separate slots and merged
///     in ordinal source-id order, every attribution picks the first evaluation sentence in file order then ordinal id,
///     and the rows are sorted. Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class CorpusLeakageAudit
{
    /// <summary>The version of the audit rules; recorded so a rule change invalidates the table.</summary>
    public const int AuditVersion = 1;

    /// <summary>The file name of the removals in the data folder.</summary>
    public const string FileName = "cluster-removals.tsv";

    /// <summary>The header line of the removals.</summary>
    public const string Header = "source_id\tparagraph\tsentence\trule\teval_file\teval_id\ttext_sha256";

    /// <summary>The rule names in the order they apply.</summary>
    public static readonly IReadOnlyList<string> Rules = ["exact", "near", "contains"];

    /// <summary>The evaluation files in attribution order.</summary>
    public static readonly IReadOnlyList<string> EvaluationFiles =
        ["dev.tsv", "holdout-natural.tsv", "holdout-independent.tsv", TextEnDataSet.RegressionFile, TextEnDataSet.GeneralizationFile];

    /// <summary>The minimum number of words for the near and contains tests (the data check's value).</summary>
    private const int NearMinimumWords = 8;

    /// <summary>The smallest evaluation sentence the contains test looks for, in words.</summary>
    private const int ContainsMinimumWords = SentenceKeys.NgramLength;

    /// <summary>The containment numerator: a share counts when <c>hits * 5 &gt;= total * 4</c> (80 percent, in integers).</summary>
    private const int ShareNumerator = 4;

    /// <summary>The containment denominator.</summary>
    private const int ShareDenominator = 5;

    /// <summary>
    ///     Loads every evaluation sentence of the data folder.
    /// </summary>
    /// <param name="dataRoot">The data folder.</param>
    /// <returns>The sentences in file order, then ordinal id order.</returns>
    /// <exception cref="InvalidDataException">The data folder does not load cleanly (run <c>check-data</c>).</exception>
    public static IReadOnlyList<EvaluationSentence> LoadEvaluationSentences(string dataRoot)
    {
        ArgumentNullException.ThrowIfNull(dataRoot);
        var report = new CheckReport();
        var data = TextEnDataSet.Load(dataRoot, report);
        if (report.FindingCount > 0)
        {
            throw new InvalidDataException($"the data folder does not load cleanly ({report.Findings[0]}); run check-data");
        }

        var sentences = data.Rows.Where(row => EvaluationFiles.Contains(row.File)).Select(static row => new EvaluationSentence(row.File, row.Id, row.Sentence))
            .Concat(data.Regression.Select(static row => new EvaluationSentence(TextEnDataSet.RegressionFile, row.Id, row.Sentence)))
            .Concat(data.Generalization.Select(static row => new EvaluationSentence(TextEnDataSet.GeneralizationFile, row.Id, row.Sentence)));
        return Order(sentences);
    }

    /// <summary>
    ///     Hashes the evaluation files the audit read, so the record shows which evaluation data the removals were made for.
    /// </summary>
    /// <param name="dataRoot">The data folder.</param>
    /// <returns>The SHA-256 of the listing <c>file TAB sha256 LF</c> in attribution order (<c>-</c> for a missing file).</returns>
    public static string EvaluationListingSha256(string dataRoot)
    {
        ArgumentNullException.ThrowIfNull(dataRoot);
        var listing = new StringBuilder();
        foreach (var file in EvaluationFiles)
        {
            var path = Path.Combine(dataRoot, file);
            listing.Append(file).Append('\t').Append(File.Exists(path) ? GutenbergBody.Sha256Hex(File.ReadAllBytes(path)) : "-").Append('\n');
        }

        return GutenbergBody.Sha256Hex(Encoding.UTF8.GetBytes(listing.ToString()));
    }

    /// <summary>
    ///     Audits the corpus against the evaluation sentences.
    /// </summary>
    /// <param name="corpus">The tokenized corpus.</param>
    /// <param name="evaluation">The evaluation sentences (any order; attribution uses file order, then ordinal id).</param>
    /// <param name="threads">The number of worker threads; at least 1. The result does not depend on it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The removals sorted by source id (ordinal), paragraph and sentence.</returns>
    public static IReadOnlyList<CorpusRemoval> Run(TokenizedCorpus corpus, IReadOnlyList<EvaluationSentence> evaluation, int threads, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentOutOfRangeException.ThrowIfLessThan(threads, 1);
        var index = new EvaluationIndex(Order(evaluation));
        var perBook = new List<CorpusRemoval>[corpus.Books.Count];
        ClusterCorpusReader.ForEachIndex(corpus.Books.Count, threads, cancellationToken, book => perBook[book] = AuditBook(corpus.Books[book], index));
        return [.. perBook.SelectMany(static removals => removals)
            .OrderBy(static removal => removal.SourceId, StringComparer.Ordinal)
            .ThenBy(static removal => removal.Paragraph)
            .ThenBy(static removal => removal.Sentence)];
    }

    /// <summary>
    ///     Computes the counts per evaluation file and rule.
    /// </summary>
    /// <param name="removals">The removals.</param>
    /// <returns>One count per evaluation file and rule, in file order then rule order.</returns>
    public static IReadOnlyList<AuditCount> Counts(IReadOnlyList<CorpusRemoval> removals)
    {
        ArgumentNullException.ThrowIfNull(removals);
        return [.. EvaluationFiles.SelectMany(file => Rules.Select(rule =>
        {
            var matching = removals.Where(removal => removal.EvalFile == file && removal.Rule == rule).ToList();
            return new AuditCount(
                file,
                rule,
                matching.Count,
                matching.Select(static removal => removal.EvalId).Distinct(StringComparer.Ordinal).Count(),
                matching.Select(static removal => removal.SourceId).Distinct(StringComparer.Ordinal).Count());
        }))];
    }

    /// <summary>
    ///     Writes the removals file.
    /// </summary>
    /// <param name="removals">The removals in sorted order.</param>
    /// <returns>The UTF-8 bytes.</returns>
    public static byte[] Write(IReadOnlyList<CorpusRemoval> removals)
    {
        ArgumentNullException.ThrowIfNull(removals);
        var builder = new StringBuilder(Header).Append('\n');
        foreach (var removal in removals)
        {
            builder.Append(removal.SourceId).Append('\t')
                .Append(removal.Paragraph.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(removal.Sentence.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(removal.Rule).Append('\t').Append(removal.EvalFile).Append('\t').Append(removal.EvalId).Append('\t')
                .Append(removal.TextSha256).Append('\n');
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>
    ///     Parses and validates the removals file.
    /// </summary>
    /// <param name="bytes">The file bytes.</param>
    /// <returns>The removals.</returns>
    /// <exception cref="InvalidDataException">The header, a column count, a number, a rule, a file name or a hash is invalid, or the rows are not unique and sorted.</exception>
    public static IReadOnlyList<CorpusRemoval> Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var text = Encoding.UTF8.GetString(bytes);
        if (text.Contains('\r') || !text.EndsWith('\n'))
        {
            throw new InvalidDataException($"{FileName} must use LF line endings and end with LF");
        }

        var lines = text[..^1].Split('\n');
        if (lines[0] != Header)
        {
            throw new InvalidDataException($"{FileName}: the header is not '{Header.Replace("\t", "<TAB>", StringComparison.Ordinal)}'");
        }

        var removals = new List<CorpusRemoval>(lines.Length - 1);
        for (var line = 1; line < lines.Length; line++)
        {
            var removal = ParseRow(lines[line], line + 1);
            if (removals.Count > 0 && Compare(removals[^1], removal) >= 0)
            {
                throw new InvalidDataException($"{FileName}:{line + 1}: the rows must be unique and sorted by source, paragraph and sentence");
            }

            removals.Add(removal);
        }

        return removals;
    }

    /// <summary>
    ///     Orders evaluation sentences for attribution: file order, then ordinal id.
    /// </summary>
    /// <param name="sentences">The sentences.</param>
    /// <returns>The ordered sentences.</returns>
    private static List<EvaluationSentence> Order(IEnumerable<EvaluationSentence> sentences) =>
    [
        .. sentences.OrderBy(static sentence => FileRank(sentence.File)).ThenBy(static sentence => sentence.Id, StringComparer.Ordinal),
    ];

    /// <summary>
    ///     Gets the attribution rank of an evaluation file.
    /// </summary>
    /// <param name="file">The file name.</param>
    /// <returns>Its position in <see cref="EvaluationFiles"/>, or the count for an unknown file.</returns>
    private static int FileRank(string file)
    {
        var rank = 0;
        while (rank < EvaluationFiles.Count && !string.Equals(EvaluationFiles[rank], file, StringComparison.Ordinal))
        {
            rank++;
        }

        return rank;
    }

    /// <summary>
    ///     Audits one book.
    /// </summary>
    /// <param name="book">The book.</param>
    /// <param name="index">The evaluation index.</param>
    /// <returns>The book's removals.</returns>
    private static List<CorpusRemoval> AuditBook(CorpusBook book, EvaluationIndex index)
    {
        var removals = new List<CorpusRemoval>();
        for (var p = 0; p < book.Paragraphs.Count; p++)
        {
            var paragraph = book.Paragraphs[p];
            var texts = new string[paragraph.Sentences.Count];
            var grams = new HashSet<string>[paragraph.Sentences.Count];
            var matched = new (string Rule, EvaluationSentence Eval)?[paragraph.Sentences.Count];

            // Exact and near, sentence by sentence
            for (var s = 0; s < texts.Length; s++)
            {
                texts[s] = paragraph.SentenceText(s);
                grams[s] = SentenceKeys.Ngrams(texts[s]);
                matched[s] = MatchSentence(texts[s], grams[s], index);
            }

            // Contains: evaluation sentences quoted inside this paragraph remove the sentences that share their 5-grams
            foreach (var eval in Contained(paragraph.Text, grams, index))
            {
                for (var s = 0; s < texts.Length; s++)
                {
                    if (matched[s] is null && grams[s].Overlaps(index.Grams[eval]))
                    {
                        matched[s] = ("contains", index.Sentences[eval]);
                    }
                }
            }

            for (var s = 0; s < texts.Length; s++)
            {
                if (matched[s] is { } match)
                {
                    removals.Add(new CorpusRemoval(
                        book.SourceId,
                        p,
                        s,
                        match.Rule,
                        match.Eval.File,
                        match.Eval.Id,
                        GutenbergBody.Sha256Hex(Encoding.UTF8.GetBytes(texts[s]))));
                }
            }
        }

        return removals;
    }

    /// <summary>
    ///     Applies the exact and near rules to one corpus sentence.
    /// </summary>
    /// <param name="text">The sentence text.</param>
    /// <param name="grams">The sentence's distinct 5-grams.</param>
    /// <param name="index">The evaluation index.</param>
    /// <returns>The rule and the first matching evaluation sentence, or null.</returns>
    private static (string Rule, EvaluationSentence Eval)? MatchSentence(string text, HashSet<string> grams, EvaluationIndex index)
    {
        var key = SentenceKeys.Key(text);
        if (key.Length > 0 && index.Keys.TryGetValue(key, out var exact))
        {
            return ("exact", index.Sentences[exact]);
        }

        if (grams.Count == 0 || SentenceKeys.Words(text).Count < NearMinimumWords)
        {
            return null;
        }

        var hits = 0;
        var first = int.MaxValue;
        foreach (var gram in grams)
        {
            if (index.FirstOwner.TryGetValue(gram, out var owner))
            {
                hits++;
                first = Math.Min(first, owner);
            }
        }

        return hits * ShareDenominator >= grams.Count * ShareNumerator ? ("near", index.Sentences[first]) : null;
    }

    /// <summary>
    ///     Finds the evaluation sentences quoted in a paragraph.
    /// </summary>
    /// <param name="paragraph">The paragraph text.</param>
    /// <param name="sentenceGrams">The distinct 5-grams of each sentence of the paragraph.</param>
    /// <param name="index">The evaluation index.</param>
    /// <returns>The indexes of the quoted evaluation sentences in attribution order.</returns>
    private static List<int> Contained(string paragraph, HashSet<string>[] sentenceGrams, EvaluationIndex index)
    {
        var result = new SortedSet<int>();

        // Long evaluation sentences: 80 percent of their 5-grams anywhere in the paragraph
        var hits = new Dictionary<int, int>();
        foreach (var gram in SentenceKeys.Ngrams(paragraph))
        {
            if (index.Owners.TryGetValue(gram, out var owners))
            {
                foreach (var owner in owners)
                {
                    hits[owner] = hits.GetValueOrDefault(owner) + 1;
                }
            }
        }

        foreach (var (eval, count) in hits)
        {
            if (index.WordCounts[eval] >= NearMinimumWords && count * ShareDenominator >= index.Grams[eval].Count * ShareNumerator)
            {
                result.Add(eval);
            }
        }

        // Short evaluation sentences: all of their 5-grams inside one corpus sentence
        foreach (var grams in sentenceGrams)
        {
            var sentenceHits = new Dictionary<int, int>();
            foreach (var gram in grams)
            {
                if (index.Owners.TryGetValue(gram, out var owners))
                {
                    foreach (var owner in owners.Where(owner => index.WordCounts[owner] < NearMinimumWords))
                    {
                        sentenceHits[owner] = sentenceHits.GetValueOrDefault(owner) + 1;
                    }
                }
            }

            foreach (var (eval, count) in sentenceHits)
            {
                if (count == index.Grams[eval].Count)
                {
                    result.Add(eval);
                }
            }
        }

        return [.. result];
    }

    /// <summary>
    ///     Parses one removals row.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="number">The 1-based line number.</param>
    /// <returns>The removal.</returns>
    private static CorpusRemoval ParseRow(string line, int number)
    {
        var cells = line.Split('\t');
        if (cells.Length != 7
            || !int.TryParse(cells[1], NumberStyles.None, CultureInfo.InvariantCulture, out var paragraph)
            || !int.TryParse(cells[2], NumberStyles.None, CultureInfo.InvariantCulture, out var sentence)
            || !Rules.Contains(cells[3])
            || !EvaluationFiles.Contains(cells[4])
            || cells[0].Length == 0
            || cells[5].Length == 0
            || cells[6].Length != 64
            || !cells[6].All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f')))
        {
            throw new InvalidDataException($"{FileName}:{number}: the row is not 'source_id, paragraph, sentence, rule, eval_file, eval_id, text_sha256'");
        }

        return new CorpusRemoval(cells[0], paragraph, sentence, cells[3], cells[4], cells[5], cells[6]);
    }

    /// <summary>
    ///     Compares two removals by source id (ordinal), paragraph and sentence.
    /// </summary>
    /// <param name="x">The first removal.</param>
    /// <param name="y">The second removal.</param>
    /// <returns>The comparison result.</returns>
    private static int Compare(CorpusRemoval x, CorpusRemoval y)
    {
        var bySource = string.CompareOrdinal(x.SourceId, y.SourceId);
        if (bySource != 0)
        {
            return bySource;
        }

        return x.Paragraph != y.Paragraph ? x.Paragraph.CompareTo(y.Paragraph) : x.Sentence.CompareTo(y.Sentence);
    }

    /// <summary>The evaluation sentences indexed for the three rules; immutable after construction, so books can share it.</summary>
    private sealed class EvaluationIndex
    {
        /// <summary>
        ///     Builds the index.
        /// </summary>
        /// <param name="sentences">The evaluation sentences in attribution order.</param>
        public EvaluationIndex(List<EvaluationSentence> sentences)
        {
            Sentences = sentences;
            Grams = new HashSet<string>[sentences.Count];
            WordCounts = new int[sentences.Count];
            for (var i = 0; i < sentences.Count; i++)
            {
                var key = SentenceKeys.Key(sentences[i].Sentence);
                if (key.Length > 0)
                {
                    Keys.TryAdd(key, i);
                }

                Grams[i] = SentenceKeys.Ngrams(sentences[i].Sentence);
                WordCounts[i] = SentenceKeys.Words(sentences[i].Sentence).Count;
                foreach (var gram in Grams[i])
                {
                    FirstOwner.TryAdd(gram, i);
                    if (WordCounts[i] >= ContainsMinimumWords)
                    {
                        if (!Owners.TryGetValue(gram, out var owners))
                        {
                            owners = [];
                            Owners.Add(gram, owners);
                        }

                        owners.Add(i);
                    }
                }
            }
        }

        /// <summary>Gets the sentences in attribution order.</summary>
        public List<EvaluationSentence> Sentences { get; }

        /// <summary>Gets the first sentence of each exact-match key.</summary>
        public Dictionary<string, int> Keys { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the first sentence of each 5-gram.</summary>
        public Dictionary<string, int> FirstOwner { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets every sentence of each 5-gram, in attribution order.</summary>
        public Dictionary<string, List<int>> Owners { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the distinct 5-grams of each sentence.</summary>
        public HashSet<string>[] Grams { get; }

        /// <summary>Gets the word count of each sentence.</summary>
        public int[] WordCounts { get; }
    }
}
