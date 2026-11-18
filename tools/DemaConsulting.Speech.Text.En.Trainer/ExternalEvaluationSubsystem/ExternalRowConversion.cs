using System.Text;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem;

/// <summary>
///     The stable names of the reasons an external row is skipped, as they appear in the console output and the record.
/// </summary>
internal static class SkipReasons
{
    /// <summary>The row's label has no entry in the id-to-id mapping.</summary>
    public const string UnmappedLabel = "unmapped-label";

    /// <summary>The row's target word is not a form of the reading inventory.</summary>
    public const string UnknownForm = "unknown-form";

    /// <summary>The mapped reading is not a candidate of the target's form.</summary>
    public const string ReadingNotCandidate = "reading-not-candidate";

    /// <summary>The row's target span cannot be placed on its sentence as the target word.</summary>
    public const string BadOffsets = "bad-offsets";

    /// <summary>The row is a verbatim copy of a row of another evaluated source.</summary>
    public const string DuplicateOfWhd = "duplicate-of-whd";

    /// <summary>Gets every reason in the fixed order the record lists them.</summary>
    public static IReadOnlyList<string> All { get; } = [BadOffsets, DuplicateOfWhd, ReadingNotCandidate, UnknownForm, UnmappedLabel];
}

/// <summary>
///     One external row as an adapter reads it: the source's own label, the sentence and the target span in UTF-16 units.
/// </summary>
/// <param name="Id">The row id (a position in the pinned files; never published).</param>
/// <param name="Label">The source's own label of the row (a WHD word id, or a heteronym-bench word and reading key).</param>
/// <param name="Sentence">The sentence text.</param>
/// <param name="Start">The UTF-16 offset of the target's first character.</param>
/// <param name="End">The UTF-16 offset just after the target's last character.</param>
/// <param name="Stratum">The stratum of the row (a source column value of the evaluation).</param>
internal sealed record ExternalRecord(string Id, string Label, string Sentence, int Start, int End, string Stratum);

/// <summary>
///     What an adapter read from one pinned source: the usable records and its own skip and conversion accounting.
/// </summary>
/// <param name="Records">The records that passed the adapter's checks, in file order.</param>
/// <param name="Skipped">The rows the adapter skipped, by reason.</param>
/// <param name="NonAsciiRows">The rows read whose sentence has a character outside ASCII.</param>
/// <param name="OffsetsConverted">The rows whose byte offsets were converted to character offsets and differ from them.</param>
/// <param name="OffsetsRepaired">The rows whose offsets did not slice to the target word and were repaired.</param>
internal sealed record ExternalReadResult(
    IReadOnlyList<ExternalRecord> Records,
    IReadOnlyDictionary<string, int> Skipped,
    int NonAsciiRows,
    int OffsetsConverted,
    int OffsetsRepaired)
{
    /// <summary>Gets the number of rows read: the usable records and every skipped row.</summary>
    public int RowsRead => Records.Count + Skipped.Values.Sum();
}

/// <summary>
///     The accounting of one converted set.
/// </summary>
/// <param name="Read">The rows read from the source.</param>
/// <param name="Used">The rows evaluated.</param>
/// <param name="Skipped">The skipped rows by reason (every reason is present, possibly zero).</param>
/// <param name="NonAsciiRows">The rows read whose sentence has a non-ASCII character.</param>
/// <param name="OffsetsConverted">The rows whose byte offsets were converted and differ.</param>
/// <param name="OffsetsRepaired">The rows whose offsets were repaired.</param>
/// <param name="ExactOverlap">The evaluated rows whose sentence equals a training or development sentence after normalization (kept, and reported).</param>
internal sealed record ConversionCounts(
    int Read,
    int Used,
    IReadOnlyDictionary<string, int> Skipped,
    int NonAsciiRows,
    int OffsetsConverted,
    int OffsetsRepaired,
    int ExactOverlap);

/// <summary>
///     The rows of one external set in the internal row format, with the accounting of the conversion.
/// </summary>
/// <param name="Name">The set name.</param>
/// <param name="Rows">The rows to evaluate, in source order.</param>
/// <param name="Counts">The conversion accounting.</param>
/// <param name="ClusterBySentence">Whether rows of one sentence form one bootstrap cluster (otherwise each row is its own cluster).</param>
internal sealed record ExternalRowSet(string Name, IReadOnlyList<LabeledRow> Rows, ConversionCounts Counts, bool ClusterBySentence)
{
    /// <summary>
    ///     Names the bootstrap cluster of a row of this set.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The cluster name.</returns>
    public string ClusterOf(LabeledRow row) => ClusterBySentence ? "s:" + row.Sentence : "r:" + row.Id;
}

/// <summary>
///     Converts UTF-8 byte offsets of a sentence to UTF-16 character offsets.
/// </summary>
internal static class ByteOffsets
{
    /// <summary>
    ///     Converts a byte span of a sentence's UTF-8 encoding to the matching UTF-16 span.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="byteStart">The offset of the first byte of the span.</param>
    /// <param name="byteEnd">The offset just after the last byte of the span.</param>
    /// <param name="start">The UTF-16 offset of the span's first character.</param>
    /// <param name="end">The UTF-16 offset just after the span's last character.</param>
    /// <returns>
    ///     <see langword="true"/> when the span is non-empty, inside the sentence and both its ends fall between two
    ///     characters; otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryToCharOffsets(string sentence, int byteStart, int byteEnd, out int start, out int end)
    {
        ArgumentNullException.ThrowIfNull(sentence);
        start = -1;
        end = -1;
        if (byteStart < 0 || byteEnd <= byteStart)
        {
            return false;
        }

        var bytePosition = 0;
        var charPosition = 0;
        foreach (var rune in sentence.EnumerateRunes())
        {
            if (bytePosition == byteStart)
            {
                start = charPosition;
            }

            if (bytePosition == byteEnd)
            {
                end = charPosition;
                break;
            }

            bytePosition += rune.Utf8SequenceLength;
            charPosition += rune.Utf16SequenceLength;
            if (bytePosition > byteEnd)
            {
                break;
            }
        }

        if (bytePosition == byteEnd)
        {
            end = charPosition;
        }

        return start >= 0 && end > start;
    }

    /// <summary>
    ///     Determines whether a sentence has a character outside ASCII.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <returns><see langword="true"/> when any character is above U+007F.</returns>
    public static bool HasNonAscii(string sentence)
    {
        ArgumentNullException.ThrowIfNull(sentence);
        return sentence.Any(static c => c > '\u007F');
    }
}

/// <summary>
///     A mapping from a source's own labels to the reading ids of the inventory (ids only, no text).
/// </summary>
internal sealed class ReadingIdMap
{
    /// <summary>The header of the WHD mapping file.</summary>
    public const string WhdHeader = "whd_wordid\treading_id";

    /// <summary>The header of the heteronym-bench mapping file.</summary>
    public const string BenchHeader = "bench_key\treading_id";

    /// <summary>The reading id of each label.</summary>
    private readonly Dictionary<string, string> _readings;

    /// <summary>
    ///     Initializes a map.
    /// </summary>
    /// <param name="readings">The reading id of each label.</param>
    private ReadingIdMap(Dictionary<string, string> readings) => _readings = readings;

    /// <summary>Gets the number of labels in the map.</summary>
    public int Count => _readings.Count;

    /// <summary>
    ///     Loads a mapping file strictly.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="expectedHeader">The exact header line.</param>
    /// <param name="inventory">The inventory every mapped reading id must belong to.</param>
    /// <returns>The map.</returns>
    /// <exception cref="InvalidDataException">The file is missing or is not UTF-8, its header or a row is malformed, a label repeats, or a reading id is not in the inventory.</exception>
    public static ReadingIdMap Load(string path, string expectedHeader, ReadingInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(expectedHeader);
        ArgumentNullException.ThrowIfNull(inventory);
        var name = Path.GetFileName(path);
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"{name}: the mapping file was not found");
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path));
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException($"{name}: the file is not valid UTF-8");
        }

        var lines = text.Split('\n');
        if (lines[0] != expectedHeader)
        {
            throw new InvalidDataException($"{name}: the header is not '{expectedHeader.Replace("\t", "<TAB>", StringComparison.Ordinal)}'");
        }

        var readings = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < lines.Length; i++)
        {
            if (i == lines.Length - 1 && lines[i].Length == 0)
            {
                break;
            }

            var cells = lines[i].Split('\t');
            if (cells.Length != 2 || cells[0].Length == 0 || cells[1].Length == 0)
            {
                throw new InvalidDataException($"{name}:{i + 1}: expected two non-empty cells");
            }

            if (!inventory.TryGetReadingIndex(cells[1], out _))
            {
                throw new InvalidDataException($"{name}:{i + 1}: the reading id '{cells[1]}' for the label '{cells[0]}' is not in the reading inventory");
            }

            if (!readings.TryAdd(cells[0], cells[1]))
            {
                throw new InvalidDataException($"{name}:{i + 1}: the label '{cells[0]}' is mapped more than once");
            }
        }

        return new ReadingIdMap(readings);
    }

    /// <summary>
    ///     Looks up the reading id of a label.
    /// </summary>
    /// <param name="label">The source's own label.</param>
    /// <param name="readingId">The reading id when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the label is mapped.</returns>
    public bool TryGet(string label, out string readingId)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (_readings.TryGetValue(label, out var found))
        {
            readingId = found;
            return true;
        }

        readingId = string.Empty;
        return false;
    }
}

/// <summary>
///     Converts the records an adapter read into internal labeled rows, mapping labels to reading ids and accounting for
///     every skipped row.
/// </summary>
internal static class ExternalRowConverter
{
    /// <summary>
    ///     Converts one source's records.
    /// </summary>
    /// <param name="setName">The set name.</param>
    /// <param name="read">What the adapter read.</param>
    /// <param name="map">The label mapping.</param>
    /// <param name="inventory">The reading inventory.</param>
    /// <param name="trainDevKeys">The normalized sentence keys (<see cref="SentenceKeys.Key"/>) of the training and development rows.</param>
    /// <param name="clusterBySentence">Whether rows of one sentence resample together.</param>
    /// <returns>The rows to evaluate and the accounting; the rows read equal the rows used plus all skipped rows.</returns>
    public static ExternalRowSet Convert(
        string setName,
        ExternalReadResult read,
        ReadingIdMap map,
        ReadingInventory inventory,
        ISet<string> trainDevKeys,
        bool clusterBySentence)
    {
        ArgumentNullException.ThrowIfNull(setName);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(trainDevKeys);

        var skipped = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var reason in SkipReasons.All)
        {
            skipped[reason] = read.Skipped.GetValueOrDefault(reason);
        }

        var rows = new List<LabeledRow>(read.Records.Count);
        var overlap = 0;
        foreach (var record in read.Records)
        {
            var reason = Check(record, map, inventory, out var readingId);
            if (reason is not null)
            {
                skipped[reason]++;
                continue;
            }

            rows.Add(new LabeledRow(record.Id, readingId, record.Sentence, record.Start, record.End, record.Stratum));
            overlap += trainDevKeys.Contains(SentenceKeys.Key(record.Sentence)) ? 1 : 0;
        }

        var counts = new ConversionCounts(read.RowsRead, rows.Count, skipped, read.NonAsciiRows, read.OffsetsConverted, read.OffsetsRepaired, overlap);
        return new ExternalRowSet(setName, rows, counts, clusterBySentence);
    }

    /// <summary>
    ///     Builds the set of normalized sentence keys of the training and development rows.
    /// </summary>
    /// <param name="corpus">The training corpus (it holds no holdout row).</param>
    /// <returns>The keys.</returns>
    public static HashSet<string> TrainDevKeys(TrainingCorpus corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in corpus.Train.Concat(corpus.Dev))
        {
            keys.Add(SentenceKeys.Key(row.Row.Sentence));
        }

        return keys;
    }

    /// <summary>
    ///     Decides whether a record is usable and finds its mapped reading id.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <param name="map">The label mapping.</param>
    /// <param name="inventory">The inventory.</param>
    /// <param name="readingId">The mapped reading id when the record is usable.</param>
    /// <returns>The skip reason, or null when the record is usable.</returns>
    private static string? Check(ExternalRecord record, ReadingIdMap map, ReadingInventory inventory, out string readingId)
    {
        readingId = string.Empty;
        if (record.Start < 0 || record.End <= record.Start || record.End > record.Sentence.Length)
        {
            return SkipReasons.BadOffsets;
        }

        if (!map.TryGet(record.Label, out readingId))
        {
            return SkipReasons.UnmappedLabel;
        }

        if (!inventory.TryFindForm(record.Sentence.AsSpan(record.Start, record.End - record.Start), out var form))
        {
            return SkipReasons.UnknownForm;
        }

        return inventory.TryGetReadingIndex(readingId, out var reading) && inventory.GetCandidates(form).Contains(reading)
            ? null
            : SkipReasons.ReadingNotCandidate;
    }
}
