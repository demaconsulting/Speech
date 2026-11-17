using System.Globalization;
using System.Text;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

/// <summary>
///     Sentence normalization used by every leakage comparison.
/// </summary>
internal static class SentenceKeys
{
    /// <summary>The word n-gram length used by the near-duplicate test.</summary>
    public const int NgramLength = 5;

    /// <summary>
    ///     Builds the exact-match key of a sentence: lower-cased (invariant culture), keeping only ASCII letters and digits.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <returns>The key.</returns>
    public static string Key(string sentence)
    {
        var builder = new StringBuilder(sentence.Length);
        foreach (var c in sentence.ToLowerInvariant().Where(IsKeyChar))
        {
            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Splits a sentence into words: the runs of ASCII letters and digits of its lower-cased text.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <returns>The words in order.</returns>
    public static IReadOnlyList<string> Words(string sentence)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var c in sentence.ToLowerInvariant())
        {
            if (IsKeyChar(c))
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return words;
    }

    /// <summary>
    ///     Builds the set of word n-grams of a sentence, each joined by single spaces.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="length">The n-gram length.</param>
    /// <returns>The distinct n-grams; empty when the sentence has fewer words than <paramref name="length"/>.</returns>
    public static HashSet<string> Ngrams(string sentence, int length = NgramLength)
    {
        var words = Words(sentence);
        var grams = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index + length <= words.Count; index++)
        {
            grams.Add(string.Join(' ', words.Skip(index).Take(length)));
        }

        return grams;
    }

    /// <summary>
    ///     Determines whether a character survives key normalization.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a lower-case ASCII letter or an ASCII digit.</returns>
    private static bool IsKeyChar(char c) => c is >= 'a' and <= 'z' or >= '0' and <= '9';
}

/// <summary>
///     An evaluation set: sentences that no training row may duplicate.
/// </summary>
/// <param name="Name">The set name (the file name without extension).</param>
/// <param name="Sentences">The sentences.</param>
internal sealed record EvaluationSet(string Name, IReadOnlyList<string> Sentences);

/// <summary>
///     One line of the leakage report: how many rows of one split overlap one evaluation set.
/// </summary>
/// <param name="EvalSet">The evaluation set name.</param>
/// <param name="EvalSentences">The number of sentences in the set.</param>
/// <param name="Split">The split name.</param>
/// <param name="Rows">The number of rows in the split.</param>
/// <param name="Exact">Rows whose key equals a sentence key of the set.</param>
/// <param name="NearDuplicate">Rows of at least eight words whose word 5-grams are mostly contained in the evaluation 5-grams.</param>
/// <param name="Share5Gram">Rows sharing at least one word 5-gram with the set.</param>
internal sealed record ExternalLeakageRow(string EvalSet, int EvalSentences, string Split, int Rows, int Exact, int NearDuplicate, int Share5Gram);

/// <summary>
///     Checks that the splits are disjoint from each other and, when evaluation sets are supplied, from those sets.
/// </summary>
/// <remarks>
///     The internal checks always run: a normalized sentence may not appear in two different data files, a natural
///     source document may not appear in two splits, and a synthetic (reading, template) pair may not appear in two
///     files. The external check runs only when an operator supplies local copies of the evaluation sets (they are
///     never committed): a row whose key equals an evaluation key, or a sentence of at least eight words whose word
///     5-grams are at least 80 percent contained in the evaluation 5-grams, is a finding. Every violation is a
///     finding on the <c>leakage</c> check.
/// </remarks>
internal sealed class LeakageChecker
{
    /// <summary>The check name used for findings.</summary>
    private const string CheckName = "leakage";

    /// <summary>The minimum number of words for the near-duplicate test.</summary>
    private const int NearMinimumWords = 8;

    /// <summary>The 5-gram containment at which a sentence counts as a near duplicate.</summary>
    private const double NearContainment = 0.8;

    /// <summary>The header of the leakage report file.</summary>
    private const string ReportHeader = "eval_set\teval_sentences\tsplit\trows\texact\tnear_duplicate\tshare_5gram";

    /// <summary>The data set under test.</summary>
    private readonly TextEnDataSet _data;

    /// <summary>The report that receives findings.</summary>
    private readonly CheckReport _report;

    /// <summary>
    ///     Creates the checker.
    /// </summary>
    /// <param name="data">The loaded data set.</param>
    /// <param name="report">The report that receives findings and informational lines.</param>
    public LeakageChecker(TextEnDataSet data, CheckReport report)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _report = report ?? throw new ArgumentNullException(nameof(report));
    }

    /// <summary>
    ///     Loads the evaluation sets of a folder: every <c>*.txt</c> file is one set (one sentence per line) and every
    ///     <c>*.tsv</c> file is one set (a header row, sentences in the column <c>sentence</c>, else <c>text</c>; no quoting).
    /// </summary>
    /// <param name="directory">The folder.</param>
    /// <returns>The sets sorted by name.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the folder does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown when the folder has no set, a TSV set has no sentence column, or a set has no sentence.</exception>
    public static IReadOnlyList<EvaluationSet> LoadEvaluationSets(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"The evaluation-set folder '{directory}' does not exist.");
        }

        var files = Directory.EnumerateFiles(directory)
            .Where(file => file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => Path.GetFileNameWithoutExtension(file), StringComparer.Ordinal)
            .ToList();
        if (files.Count == 0)
        {
            throw new InvalidDataException($"The evaluation-set folder '{directory}' contains no *.txt or *.tsv file.");
        }

        return files
            .Select(file => new EvaluationSet(Path.GetFileNameWithoutExtension(file), ReadUsableSentences(file)))
            .ToList();
    }

    /// <summary>
    ///     Determines whether a folder lies inside (or is) the data root.
    /// </summary>
    /// <param name="directory">The folder.</param>
    /// <param name="root">The data root.</param>
    /// <returns><see langword="true"/> when <paramref name="directory"/> is <paramref name="root"/> or below it.</returns>
    public static bool IsInside(string directory, string root)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(directory));
        return relative == "." || (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative));
    }

    /// <summary>
    ///     Writes the leakage report in the format of <c>leakage-report.tsv</c> (UTF-8 without byte order mark, LF).
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="rows">The report rows.</param>
    public static void WriteReport(string path, IEnumerable<ExternalLeakageRow> rows)
    {
        var builder = new StringBuilder();
        builder.Append(ReportHeader).Append('\n');
        foreach (var row in rows)
        {
            builder.Append(string.Join('\t', row.EvalSet, Format(row.EvalSentences), row.Split, Format(row.Rows), Format(row.Exact), Format(row.NearDuplicate), Format(row.Share5Gram)))
                .Append('\n');
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
    }

    /// <summary>
    ///     Runs the checks that need no evaluation data: split disjointness of sentences, documents and templates.
    /// </summary>
    public void CheckInternal()
    {
        CheckSentenceKeys();
        CheckDocuments();
        CheckTemplates();
    }

    /// <summary>
    ///     Compares every row with the evaluation sets.
    /// </summary>
    /// <param name="sets">The evaluation sets.</param>
    /// <returns>One report row per evaluation set and split, sets sorted by name and splits in file order.</returns>
    /// <exception cref="InvalidDataException">Thrown when the sets hold no sentence at all, since nothing would be checked.</exception>
    public IReadOnlyList<ExternalLeakageRow> CheckExternal(IReadOnlyList<EvaluationSet> sets)
    {
        ArgumentNullException.ThrowIfNull(sets);
        if (sets.Sum(set => set.Sentences.Count) == 0)
        {
            throw new InvalidDataException("The evaluation sets contain no sentences, so the external leakage check would evaluate nothing.");
        }

        var ordered = sets.OrderBy(set => set.Name, StringComparer.Ordinal).ToList();

        // Index the evaluation material: key -> sets, word 5-gram -> sets
        var keys = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var grams = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var index = 0; index < ordered.Count; index++)
        {
            foreach (var sentence in ordered[index].Sentences)
            {
                AddOwner(keys, SentenceKeys.Key(sentence), index);
                foreach (var gram in SentenceKeys.Ngrams(sentence))
                {
                    AddOwner(grams, gram, index);
                }
            }
        }

        var splits = TextEnDataSet.DataFiles.Select(file => file.Split).ToList();
        var counters = new int[ordered.Count, splits.Count, 3];
        var rowsPerSplit = new int[splits.Count];
        var rowsSharingAny = new int[splits.Count];
        foreach (var row in _data.Rows)
        {
            var splitIndex = splits.IndexOf(row.Split);
            rowsPerSplit[splitIndex]++;
            if (CompareRow(row, ordered, keys, grams, counters, splitIndex))
            {
                rowsSharingAny[splitIndex]++;
            }
        }

        var result = BuildRows(ordered, splits, rowsPerSplit, counters);
        ReportExternal(ordered, keys.Count, splits, rowsPerSplit, rowsSharingAny, result);
        return result;
    }

    /// <summary>
    ///     Reads the sentences of one evaluation file and rejects a file that yields none, so an empty or header-only
    ///     file can never make the external check pass with nothing evaluated.
    /// </summary>
    /// <param name="file">The file path.</param>
    /// <returns>The usable sentences, at least one.</returns>
    /// <exception cref="InvalidDataException">Thrown when the file has no usable sentence.</exception>
    private static List<string> ReadUsableSentences(string file)
    {
        var sentences = ReadEvaluationSentences(file);
        if (sentences.Count == 0)
        {
            throw new InvalidDataException($"The evaluation set '{file}' has no sentences.");
        }

        return sentences;
    }

    /// <summary>
    ///     Reads the sentences of one evaluation file.
    /// </summary>
    /// <param name="file">The file path.</param>
    /// <returns>The sentences that are not empty or whitespace only, with their original text.</returns>
    private static List<string> ReadEvaluationSentences(string file)
    {
        var lines = File.ReadLines(file, new UTF8Encoding(false)).Select(line => line.TrimEnd('\r'));
        if (!file.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase))
        {
            return lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        }

        // A TSV set names its sentence column in the header; there is no quoting
        var rows = lines.Select(line => line.Split('\t')).ToList();
        var header = rows.Count > 0 ? rows[0] : [];
        var column = Array.IndexOf(header, "sentence");
        if (column < 0)
        {
            column = Array.IndexOf(header, "text");
        }

        if (column < 0)
        {
            throw new InvalidDataException($"The evaluation set '{file}' has neither a 'sentence' nor a 'text' column.");
        }

        return rows.Skip(1).Where(cells => cells.Length > column && !string.IsNullOrWhiteSpace(cells[column])).Select(cells => cells[column]).ToList();
    }

    /// <summary>
    ///     Records that a set contains a key or n-gram.
    /// </summary>
    /// <param name="index">The index to extend.</param>
    /// <param name="key">The key or n-gram.</param>
    /// <param name="owner">The evaluation set number.</param>
    private static void AddOwner(Dictionary<string, List<int>> index, string key, int owner)
    {
        if (!index.TryGetValue(key, out var owners))
        {
            owners = [];
            index[key] = owners;
        }

        if (!owners.Contains(owner))
        {
            owners.Add(owner);
        }
    }

    /// <summary>
    ///     Compares one row with the evaluation index and updates the counters.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="sets">The evaluation sets.</param>
    /// <param name="keys">The key index.</param>
    /// <param name="grams">The n-gram index.</param>
    /// <param name="counters">The counters indexed by set, split and kind (exact, near, shared 5-gram).</param>
    /// <param name="splitIndex">The index of the row's split.</param>
    /// <returns><see langword="true"/> when the row shares at least one 5-gram with any evaluation set.</returns>
    private bool CompareRow(
        DataRow row,
        List<EvaluationSet> sets,
        Dictionary<string, List<int>> keys,
        Dictionary<string, List<int>> grams,
        int[,,] counters,
        int splitIndex)
    {
        // Exact normalized match
        if (keys.TryGetValue(SentenceKeys.Key(row.Sentence), out var exactOwners))
        {
            foreach (var owner in exactOwners)
            {
                counters[owner, splitIndex, 0]++;
                _report.Add(CheckName, row.Location, $"{row.Id}: exact match with evaluation set {sets[owner].Name}");
            }
        }

        // Shared and near-duplicate 5-grams
        var rowGrams = SentenceKeys.Ngrams(row.Sentence);
        var hits = rowGrams.Where(grams.ContainsKey).ToList();
        var sharing = hits.SelectMany(gram => grams[gram]).Distinct().Order().ToList();
        foreach (var owner in sharing)
        {
            counters[owner, splitIndex, 2]++;
        }

        if (rowGrams.Count > 0
            && SentenceKeys.Words(row.Sentence).Count >= NearMinimumWords
            && (double)hits.Count / rowGrams.Count >= NearContainment)
        {
            foreach (var owner in sharing)
            {
                counters[owner, splitIndex, 1]++;
            }

            _report.Add(CheckName, row.Location, $"{row.Id}: near duplicate of evaluation set(s) {string.Join(",", sharing.Select(owner => sets[owner].Name))}");
        }

        return sharing.Count > 0;
    }

    /// <summary>
    ///     Converts the counters to report rows.
    /// </summary>
    /// <param name="sets">The evaluation sets.</param>
    /// <param name="splits">The split names.</param>
    /// <param name="rowsPerSplit">The row count per split.</param>
    /// <param name="counters">The counters.</param>
    /// <returns>The rows.</returns>
    private static List<ExternalLeakageRow> BuildRows(List<EvaluationSet> sets, List<string> splits, int[] rowsPerSplit, int[,,] counters)
    {
        var result = new List<ExternalLeakageRow>();
        for (var set = 0; set < sets.Count; set++)
        {
            for (var split = 0; split < splits.Count; split++)
            {
                result.Add(new ExternalLeakageRow(
                    sets[set].Name,
                    sets[set].Sentences.Count,
                    splits[split],
                    rowsPerSplit[split],
                    counters[set, split, 0],
                    counters[set, split, 1],
                    counters[set, split, 2]));
            }
        }

        return result;
    }

    /// <summary>
    ///     Adds the informational lines of the external check.
    /// </summary>
    /// <param name="sets">The evaluation sets.</param>
    /// <param name="distinctKeys">The number of distinct evaluation keys.</param>
    /// <param name="splits">The split names.</param>
    /// <param name="rowsPerSplit">The row count per split.</param>
    /// <param name="rowsSharingAny">The rows sharing a 5-gram with any set, per split.</param>
    /// <param name="rows">The report rows.</param>
    private void ReportExternal(List<EvaluationSet> sets, int distinctKeys, List<string> splits, int[] rowsPerSplit, int[] rowsSharingAny, List<ExternalLeakageRow> rows)
    {
        _report.AddInfo($"evaluation sets {sets.Count} sentences {sets.Sum(set => set.Sentences.Count)} distinct keys {distinctKeys}");
        _report.AddInfo($"{"eval set",-34} {"split",-20} {"rows",6} {"exact",6} {"near",6} {"5gram",8}");
        foreach (var row in rows.Where(row => row.Exact > 0 || row.NearDuplicate > 0 || row.Share5Gram > 0))
        {
            _report.AddInfo($"{row.EvalSet,-34} {row.Split,-20} {row.Rows,6} {row.Exact,6} {row.NearDuplicate,6} {row.Share5Gram,8}");
        }

        for (var split = 0; split < splits.Count; split++)
        {
            _report.AddInfo($"rows sharing >= 1 word 5-gram with any evaluation set, {splits[split]}: {rowsSharingAny[split]} of {rowsPerSplit[split]}");
        }
    }

    /// <summary>
    ///     A normalized sentence may not occur in two different data files (repeating it inside one file is allowed).
    /// </summary>
    private void CheckSentenceKeys()
    {
        var first = new Dictionary<string, DataRow>(StringComparer.Ordinal);
        foreach (var row in _data.Rows)
        {
            var key = SentenceKeys.Key(row.Sentence);
            if (!first.TryAdd(key, row) && !string.Equals(first[key].File, row.File, StringComparison.Ordinal))
            {
                _report.Add(CheckName, row.Location, $"{row.Id}: sentence also in {first[key].File} ({first[key].Id})");
            }
        }
    }

    /// <summary>
    ///     A natural source document may not occur in two splits.
    /// </summary>
    private void CheckDocuments()
    {
        var documentOfRow = FirstKeyById(_data.HarvestProvenance);
        var splitOfDocument = new Dictionary<string, string>(StringComparer.Ordinal);
        // Only harvested rows have a source document; the source decides, so a renamed row cannot dodge the check
        foreach (var row in _data.Rows.Where(row => SourceInfo.KindOf(row.Source) == SourceKind.Harvest && documentOfRow.ContainsKey(row.Id)))
        {
            var document = documentOfRow[row.Id];
            if (!splitOfDocument.TryAdd(document, row.Split) && splitOfDocument[document] != row.Split)
            {
                _report.Add(CheckName, row.Location, $"{row.Id}: document {document} in {splitOfDocument[document]} and {row.Split}");
            }
        }
    }

    /// <summary>
    ///     A synthetic (reading, template) pair may not occur in two data files.
    /// </summary>
    private void CheckTemplates()
    {
        var templateOfRow = FirstKeyById(_data.SyntheticProvenance);
        var splitOfPair = new Dictionary<(string Reading, string Template), string>();
        // Only synthetic rows have a template; the source decides, so a renamed row cannot dodge the check
        foreach (var row in _data.Rows.Where(row => SourceInfo.KindOf(row.Source) == SourceKind.Synthetic && templateOfRow.ContainsKey(row.Id)))
        {
            var pair = (Reading: row.ReadingId, Template: templateOfRow[row.Id]);
            if (!splitOfPair.TryAdd(pair, row.Split) && splitOfPair[pair] != row.Split)
            {
                _report.Add(CheckName, row.Location, $"{row.Id}: synthetic template '{pair.Template}' for {pair.Reading} in {splitOfPair[pair]} and {row.Split}");
            }
        }
    }

    /// <summary>
    ///     Maps provenance ids to their keys, keeping the first of any duplicates.
    /// </summary>
    /// <param name="records">The provenance records.</param>
    /// <returns>The map.</returns>
    private static Dictionary<string, string> FirstKeyById(IEnumerable<ProvenanceRecord> records)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            map.TryAdd(record.Id, record.Key);
        }

        return map;
    }

    /// <summary>
    ///     Formats an integer with the invariant culture.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
}
