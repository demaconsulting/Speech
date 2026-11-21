using System.Globalization;
using System.Text;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

/// <summary>One row of <c>word-clusters.tsv</c>.</summary>
/// <param name="Word">The normalized word.</param>
/// <param name="Path">The bit-string path of the word's cluster.</param>
/// <param name="Count">The word's corpus count.</param>
internal sealed record WordClusterEntry(string Word, string Path, long Count);

/// <summary>
///     The committed word-cluster table <c>word-clusters.tsv</c>: one row per word with its cluster path and count,
///     built so that no runtime lookup can return another word's cluster.
/// </summary>
/// <remarks>
///     <para>
///     The runtime looks a word up by its 32-bit lower-case hash, not by its text. When two vocabulary words share a
///     hash, a lookup of either would return whichever entry the table kept, so <b>every</b> word of such a hash is left
///     out and counted; a word that is not in the table still matches a table hash with a probability of about the table
///     size divided by 2^32. Words with a character outside the eligible set are also left out and counted.
///     </para>
///     <para>
///     The text form is fixed: the header <c>word TAB path TAB count</c>, one row per word in ordinal word order, LF line
///     endings, UTF-8 without byte order mark. Parsing validates every rule the builder guarantees. Stateless and
///     thread-safe.
///     </para>
/// </remarks>
internal static class WordClusterTable
{
    /// <summary>The file name of the table in the data folder.</summary>
    public const string FileName = ClusterDataChecker.TableFile;

    /// <summary>The header line of the table.</summary>
    public const string Header = "word\tpath\tcount";

    /// <summary>
    ///     Builds the table rows from the clustered vocabulary.
    /// </summary>
    /// <param name="words">The vocabulary words (normalized forms) by rank.</param>
    /// <param name="paths">The path of each word, by rank.</param>
    /// <param name="counts">The corpus count of each word, by rank.</param>
    /// <param name="droppedCollisions">Receives the number of words left out because their hash is shared.</param>
    /// <param name="droppedIneligible">Receives the number of words left out because of an ineligible character or length.</param>
    /// <returns>The rows in ordinal word order.</returns>
    /// <exception cref="ArgumentException">The three lists differ in length or a word is repeated.</exception>
    public static IReadOnlyList<WordClusterEntry> Build(
        IReadOnlyList<string> words,
        IReadOnlyList<string> paths,
        IReadOnlyList<long> counts,
        out int droppedCollisions,
        out int droppedIneligible)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(counts);
        if (paths.Count != words.Count || counts.Count != words.Count || words.Distinct(StringComparer.Ordinal).Count() != words.Count)
        {
            throw new ArgumentException("every word needs one path and one count, and words must be unique", nameof(words));
        }

        // Ineligible words first, then every word of a hash shared by two or more eligible words
        var eligible = Enumerable.Range(0, words.Count).Where(rank => IsEligibleWord(words[rank])).ToList();
        droppedIneligible = words.Count - eligible.Count;
        var shared = eligible.GroupBy(rank => FeatureHashing.Fnv1a(words[rank]))
            .Where(static group => group.Count() > 1)
            .SelectMany(static group => group)
            .ToHashSet();
        droppedCollisions = shared.Count;
        return [.. eligible.Where(rank => !shared.Contains(rank))
            .Select(rank => new WordClusterEntry(words[rank], paths[rank], counts[rank]))
            .OrderBy(static entry => entry.Word, StringComparer.Ordinal)];
    }

    /// <summary>
    ///     Writes the table text.
    /// </summary>
    /// <param name="entries">The rows in ordinal word order.</param>
    /// <returns>The UTF-8 bytes of the table.</returns>
    public static byte[] Write(IReadOnlyList<WordClusterEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var builder = new StringBuilder();
        builder.Append(Header).Append('\n');
        foreach (var entry in entries)
        {
            builder.Append(entry.Word).Append('\t').Append(entry.Path).Append('\t')
                .Append(entry.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>
    ///     Parses and validates the table text.
    /// </summary>
    /// <param name="bytes">The table bytes.</param>
    /// <param name="maximumPaths">The largest number of distinct paths allowed (the number of clusters).</param>
    /// <param name="minimumCount">The smallest count allowed.</param>
    /// <returns>The rows.</returns>
    /// <exception cref="InvalidDataException">
    ///     The text is not UTF-8 with LF endings and the exact header; a row has the wrong column count, an ineligible word,
    ///     a path that is not a bit string or a count below the minimum; the words are not unique and in ordinal order; a
    ///     path is a prefix of another; there are more distinct paths than allowed; or two words share a hash.
    /// </exception>
    public static IReadOnlyList<WordClusterEntry> Parse(byte[] bytes, int maximumPaths, long minimumCount)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException($"{FileName} is not valid UTF-8");
        }

        if (text.Contains('\r') || !text.EndsWith('\n') || (bytes.Length >= 3 && bytes[0] == 0xEF))
        {
            throw new InvalidDataException($"{FileName} must use LF line endings, end with LF and have no byte order mark");
        }

        var lines = text[..^1].Split('\n');
        if (lines[0] != Header)
        {
            throw new InvalidDataException($"{FileName}: the header is not '{Header.Replace("\t", "<TAB>", StringComparison.Ordinal)}'");
        }

        var entries = new List<WordClusterEntry>(lines.Length - 1);
        for (var index = 1; index < lines.Length; index++)
        {
            entries.Add(ParseRow(lines[index], index + 1, minimumCount, entries.Count > 0 ? entries[^1].Word : null));
        }

        CheckPaths(entries, maximumPaths);
        var collision = entries.GroupBy(static entry => FeatureHashing.Fnv1a(entry.Word)).FirstOrDefault(static group => group.Count() > 1);
        if (collision is not null)
        {
            throw new InvalidDataException($"{FileName}: the words {string.Join(", ", collision.Select(static entry => entry.Word))} share a lookup hash");
        }

        return entries;
    }

    /// <summary>
    ///     Determines whether a word may be a table key.
    /// </summary>
    /// <param name="word">The normalized word.</param>
    /// <returns><see langword="true"/> for 1 to 40 eligible characters.</returns>
    public static bool IsEligibleWord(string word) =>
        word.Length is > 0 and <= ClusterCorpusReader.MaximumFormLength && word.All(ClusterCorpusReader.IsEligible);

    /// <summary>
    ///     Parses one row.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="number">The 1-based line number.</param>
    /// <param name="minimumCount">The smallest count allowed.</param>
    /// <param name="previous">The previous word, or null.</param>
    /// <returns>The row.</returns>
    private static WordClusterEntry ParseRow(string line, int number, long minimumCount, string? previous)
    {
        var cells = line.Split('\t');
        if (cells.Length != 3)
        {
            throw new InvalidDataException($"{FileName}:{number}: expected 3 columns");
        }

        if (!IsEligibleWord(cells[0]))
        {
            throw new InvalidDataException($"{FileName}:{number}: the word has an ineligible character or length");
        }

        if (previous is not null && string.CompareOrdinal(previous, cells[0]) >= 0)
        {
            throw new InvalidDataException($"{FileName}:{number}: the words must be unique and in ordinal order");
        }

        if (cells[1].Length == 0 || cells[1].Any(static c => c is not ('0' or '1')))
        {
            throw new InvalidDataException($"{FileName}:{number}: the path is not a bit string");
        }

        if (!long.TryParse(cells[2], NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < minimumCount
            || !string.Equals(count.ToString(CultureInfo.InvariantCulture), cells[2], StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{FileName}:{number}: the count is not an integer of at least {minimumCount}");
        }

        return new WordClusterEntry(cells[0], cells[1], count);
    }

    /// <summary>
    ///     Checks that the distinct paths are prefix-free and not too many.
    /// </summary>
    /// <param name="entries">The rows.</param>
    /// <param name="maximumPaths">The largest number of distinct paths.</param>
    private static void CheckPaths(List<WordClusterEntry> entries, int maximumPaths)
    {
        // In ordinal order, a path that is a prefix of another is directly followed by one it prefixes
        var paths = entries.Select(static entry => entry.Path).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (paths.Count > maximumPaths)
        {
            throw new InvalidDataException($"{FileName}: {paths.Count} distinct paths but at most {maximumPaths} clusters");
        }

        for (var i = 1; i < paths.Count; i++)
        {
            if (paths[i].StartsWith(paths[i - 1], StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{FileName}: the path {paths[i - 1]} is a prefix of {paths[i]}");
            }
        }
    }
}
