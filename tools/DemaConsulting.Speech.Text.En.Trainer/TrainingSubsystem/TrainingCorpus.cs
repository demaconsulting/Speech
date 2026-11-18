using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

/// <summary>
///     One labeled row of a data file: a sentence, the span of the target word in it and the reading it has there.
/// </summary>
/// <param name="Id">The row id.</param>
/// <param name="ReadingId">The labeled reading id.</param>
/// <param name="Sentence">The sentence text.</param>
/// <param name="Start">The UTF-16 offset of the target's first character in <paramref name="Sentence"/>.</param>
/// <param name="End">The UTF-16 offset just after the target's last character.</param>
/// <param name="Source">The source column, which names the stratum of the row (<c>harvest-*</c> rows are natural text).</param>
internal sealed record LabeledRow(string Id, string ReadingId, string Sentence, int Start, int End, string Source)
{
    /// <summary>Gets a value indicating whether the row is natural (harvested) text rather than synthetic or independently written text.</summary>
    public bool IsNatural => Source.StartsWith("harvest-", StringComparison.Ordinal);
}

/// <summary>
///     A labeled row prepared the way the analyzer would see it: aligned to the runtime tokenization, tagged, and
///     reduced to the hashed features of its target.
/// </summary>
/// <remarks>
///     A row is <em>aligned</em> when its target span is exactly one token of the runtime tokenizer; only an aligned
///     row has features. Immutable after construction.
/// </remarks>
internal sealed class PreparedRow
{
    /// <summary>
    ///     Initializes a prepared row.
    /// </summary>
    /// <param name="row">The labeled row.</param>
    /// <param name="formIndex">The inventory form of the target.</param>
    /// <param name="labelIndex">The inventory index of the labeled reading.</param>
    /// <param name="candidates">The candidate reading indexes of the form, in table order.</param>
    /// <param name="upos">The tagger's part of speech of the target, or <see langword="null"/> when the row is not aligned.</param>
    /// <param name="conjoined">The conjoined feature hashes; empty when the row is not aligned.</param>
    /// <param name="shared">The shared feature hashes; empty when the row is not aligned.</param>
    /// <param name="context">The classifier context of the target's sentence, or <see langword="null"/> when not kept or not aligned.</param>
    /// <param name="target">The index of the target within <paramref name="context"/>.</param>
    public PreparedRow(
        LabeledRow row,
        int formIndex,
        int labelIndex,
        IReadOnlyList<int> candidates,
        string? upos,
        uint[] conjoined,
        uint[] shared,
        ContextToken[]? context,
        int target)
    {
        Row = row;
        FormIndex = formIndex;
        LabelIndex = labelIndex;
        Candidates = candidates;
        Upos = upos;
        Conjoined = conjoined;
        Shared = shared;
        Context = context;
        Target = target;
    }

    /// <summary>Gets the labeled row.</summary>
    public LabeledRow Row { get; }

    /// <summary>Gets the inventory form of the target.</summary>
    public int FormIndex { get; }

    /// <summary>Gets the inventory index of the labeled reading.</summary>
    public int LabelIndex { get; }

    /// <summary>Gets the candidate reading indexes of the form, in table order.</summary>
    public IReadOnlyList<int> Candidates { get; }

    /// <summary>Gets the tagger's part of speech of the target; null when the row is not aligned.</summary>
    public string? Upos { get; }

    /// <summary>Gets the conjoined feature hashes of the target.</summary>
    public uint[] Conjoined { get; }

    /// <summary>Gets the shared feature hashes of the target.</summary>
    public uint[] Shared { get; }

    /// <summary>Gets the classifier context of the target's sentence, or null.</summary>
    public ContextToken[]? Context { get; }

    /// <summary>Gets the index of the target within <see cref="Context"/>.</summary>
    public int Target { get; }

    /// <summary>Gets a value indicating whether the target span is exactly one token of the runtime tokenization.</summary>
    public bool IsAligned => Upos is not null;

    /// <summary>Gets a value indicating whether the form has more than one candidate reading (a one-candidate form carries no signal).</summary>
    public bool IsAmbiguous => Candidates.Count > 1;
}

/// <summary>
///     Reads labeled data files and prepares their rows with the analyzer's own tokenizer, tagger and feature
///     extractor.
/// </summary>
internal static class RowPreparer
{
    /// <summary>
    ///     Reads a labeled data file strictly.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="name">The name shown in errors.</param>
    /// <returns>The rows in file order.</returns>
    /// <exception cref="InvalidDataException">The file is missing, malformed or has a row whose span is outside its sentence.</exception>
    public static IReadOnlyList<LabeledRow> ReadRows(string path, string name)
    {
        var report = new CheckReport(10);
        var table = TsvTable.Load(path, name, report, new TsvLayout(ExpectedHeader: TextEnDataSet.RowsHeader));
        if (report.FindingCount > 0)
        {
            throw new InvalidDataException($"{name}: {report.Findings[0].Location}: {report.Findings[0].Message}");
        }

        var rows = new List<LabeledRow>(table.Rows.Count);
        foreach (var cells in table.Rows)
        {
            // Both offsets must be plain non-negative integers (NumberStyles.None admits no sign, and the explicit
            // range test keeps that guarantee independent of the parse style), compared without arithmetic so no
            // combination of offsets can overflow; later stages slice the sentence with them unchecked.
            if (!int.TryParse(cells[4], NumberStyles.None, CultureInfo.InvariantCulture, out var start)
                || !int.TryParse(cells[5], NumberStyles.None, CultureInfo.InvariantCulture, out var end)
                || start < 0
                || end < 0
                || start >= end
                || end > cells[3].Length)
            {
                throw new InvalidDataException($"{name}:{cells.LineNumber}: the target span is not inside the sentence");
            }

            rows.Add(new LabeledRow(cells[0], cells[2], cells[3], start, end, cells[6]));
        }

        return rows;
    }

    /// <summary>
    ///     Prepares one row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="inventory">The reading inventory.</param>
    /// <param name="adapter">The part-of-speech seam that tokenizes and tags the sentence.</param>
    /// <param name="keepContext">Whether to keep the classifier context in the result (for scoring through the runtime classifier).</param>
    /// <returns>The prepared row; not aligned when the target span is not exactly one runtime token.</returns>
    /// <exception cref="InvalidDataException">The target is not a form of the inventory, or the label is not one of its candidates.</exception>
    public static PreparedRow Prepare(LabeledRow row, ReadingInventory inventory, PartOfSpeechAdapter adapter, bool keepContext)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(adapter);

        // The label must be a candidate of the target's form, whatever the tokenization says.
        if (!inventory.TryFindForm(row.Sentence.AsSpan(row.Start, row.End - row.Start), out var formIndex))
        {
            throw new InvalidDataException($"row {row.Id}: the target is not a form of the reading inventory");
        }

        var candidates = inventory.GetCandidates(formIndex).ToArray();
        if (!inventory.TryGetReadingIndex(row.ReadingId, out var labelIndex) || Array.IndexOf(candidates, labelIndex) < 0)
        {
            throw new InvalidDataException($"row {row.Id}: the labeled reading is not a candidate of the target's form");
        }

        // Tokenize and tag exactly as the analyzer does, then look for the token that is exactly the target.
        var analysisText = EnglishTextAnalyzer.NeutralizePlaceholders(row.Sentence);
        var sentences = adapter.Analyze(analysisText, tag: true, CancellationToken.None);
        var length = row.End - row.Start;
        for (var s = 0; s < sentences.Count; s++)
        {
            var sentence = sentences[s];
            for (var t = 0; t < sentence.Count; t++)
            {
                if (sentence[t].Start != row.Start || sentence[t].Length != length)
                {
                    continue;
                }

                var context = PartOfSpeechAdapter.DescribeSentence(analysisText, sentence, null);
                Span<uint> conjoined = stackalloc uint[HeteronymFeatureExtractor.MaxConjoined];
                Span<uint> shared = stackalloc uint[HeteronymFeatureExtractor.MaxShared];
                HeteronymFeatureExtractor.Extract(context.AsSpan(0, sentence.Count), t, conjoined, shared, out var conjoinedCount, out var sharedCount);
                return new PreparedRow(
                    row,
                    formIndex,
                    labelIndex,
                    candidates,
                    sentence[t].Tag ?? "X",
                    conjoined[..conjoinedCount].ToArray(),
                    shared[..sharedCount].ToArray(),
                    keepContext ? context[..sentence.Count] : null,
                    t);
            }
        }

        return new PreparedRow(row, formIndex, labelIndex, candidates, null, [], [], null, -1);
    }
}

/// <summary>
///     The training and development data the trainer reads, prepared with the analyzer's own pipeline.
/// </summary>
/// <remarks>
///     <para>
///     <b>Isolation.</b> <see cref="Load(string)"/> opens exactly four files of the data folder: <c>train.tsv</c>,
///     <c>dev.tsv</c>, <c>readings.tsv</c> and <c>forms.tsv</c>. It never lists the folder and never names a holdout
///     file, so the holdout sets cannot influence a model; that is a structural property, not a convention.
///     </para>
///     <para>
///     <b>Hashes.</b> <see cref="DataSha256"/> is the SHA-256 of the canonical listing <c>name TAB sha256 LF</c> of
///     those four files in ordinal name order, so any change to a training or selection input changes it and an edit
///     of any other file of the folder does not. <see cref="FeaturesSha256"/> hashes the ordered feature stream of
///     the prepared rows, so a drift of the tagger or the extractor is visible even when the data files are
///     unchanged. Immutable after loading.
///     </para>
/// </remarks>
internal sealed class TrainingCorpus
{
    /// <summary>The training file name.</summary>
    public const string TrainFile = "train.tsv";

    /// <summary>The development file name.</summary>
    public const string DevFile = "dev.tsv";

    /// <summary>The readings table file name.</summary>
    public const string ReadingsFile = "readings.tsv";

    /// <summary>The forms table file name.</summary>
    public const string FormsFile = "forms.tsv";

    /// <summary>The four input files in ordinal name order, the order of the data hash listing.</summary>
    private static readonly string[] InputFiles = [DevFile, FormsFile, ReadingsFile, TrainFile];

    /// <summary>The strict UTF-8 decoder: invalid bytes throw, and a byte order mark is kept so the inventory parser rejects it.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    ///     Initializes a corpus from its parts.
    /// </summary>
    /// <param name="inventory">The reading inventory.</param>
    /// <param name="posHints">The part-of-speech hints of every reading, by reading index.</param>
    /// <param name="train">The prepared training rows.</param>
    /// <param name="dev">The prepared development rows.</param>
    /// <param name="dataSha256">The data hash.</param>
    /// <param name="featuresSha256">The features hash.</param>
    private TrainingCorpus(
        ReadingInventory inventory,
        IReadOnlyList<IReadOnlyList<string>> posHints,
        IReadOnlyList<PreparedRow> train,
        IReadOnlyList<PreparedRow> dev,
        byte[] dataSha256,
        byte[] featuresSha256)
    {
        Inventory = inventory;
        PosHints = posHints;
        Train = train;
        Dev = dev;
        DataSha256Bytes = dataSha256;
        FeaturesSha256Bytes = featuresSha256;
    }

    /// <summary>Gets the reading inventory built from <c>readings.tsv</c> and <c>forms.tsv</c>.</summary>
    public ReadingInventory Inventory { get; }

    /// <summary>Gets the part-of-speech hints of every reading, by reading index (the <c>|</c>-separated column, split).</summary>
    public IReadOnlyList<IReadOnlyList<string>> PosHints { get; }

    /// <summary>Gets the prepared training rows in file order.</summary>
    public IReadOnlyList<PreparedRow> Train { get; }

    /// <summary>Gets the prepared development rows in file order.</summary>
    public IReadOnlyList<PreparedRow> Dev { get; }

    /// <summary>Gets the data hash bytes.</summary>
    public byte[] DataSha256Bytes { get; }

    /// <summary>Gets the features hash bytes.</summary>
    public byte[] FeaturesSha256Bytes { get; }

    /// <summary>Gets the data hash as lower-case hex.</summary>
    public string DataSha256 => Convert.ToHexString(DataSha256Bytes).ToLowerInvariant();

    /// <summary>Gets the features hash as lower-case hex.</summary>
    public string FeaturesSha256 => Convert.ToHexString(FeaturesSha256Bytes).ToLowerInvariant();

    /// <summary>
    ///     Gets the part-of-speech package version, the version of the tagger whose tags are features.
    /// </summary>
    public static string TaggerVersion { get; } = ReadTaggerVersion();

    /// <summary>
    ///     Loads the four input files and prepares every row.
    /// </summary>
    /// <param name="dataRoot">The data folder.</param>
    /// <returns>The corpus.</returns>
    /// <exception cref="InvalidDataException">An input file is missing or malformed, or a row names a target or reading the inventory does not have.</exception>
    public static TrainingCorpus Load(string dataRoot) => Load(dataRoot, PartOfSpeechAdapter.Shared);

    /// <summary>
    ///     Loads the four input files and prepares every row with a given part-of-speech seam.
    /// </summary>
    /// <param name="dataRoot">The data folder.</param>
    /// <param name="adapter">The part-of-speech seam.</param>
    /// <returns>The corpus.</returns>
    /// <exception cref="InvalidDataException">An input file is missing or malformed, or a row names a target or reading the inventory does not have.</exception>
    public static TrainingCorpus Load(string dataRoot, PartOfSpeechAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(dataRoot);
        ArgumentNullException.ThrowIfNull(adapter);

        // Hash the raw bytes of the four inputs in ordinal name order; nothing else in the folder is opened.
        var contents = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var listing = new StringBuilder();
        foreach (var name in InputFiles)
        {
            var path = Path.Combine(dataRoot, name);
            if (!File.Exists(path))
            {
                throw new InvalidDataException($"{name}: the required file is missing");
            }

            var bytes = File.ReadAllBytes(path);
            contents[name] = bytes;
            listing.Append(name).Append('\t').Append(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()).Append('\n');
        }

        var dataSha = SHA256.HashData(Encoding.UTF8.GetBytes(listing.ToString()));

        // The inventory from the two tables, then each reading's part-of-speech hints.
        var readingsText = Decode(contents[ReadingsFile], ReadingsFile);
        var formsText = Decode(contents[FormsFile], FormsFile);
        ReadingInventory inventory;
        using (var readings = new StringReader(readingsText))
        using (var forms = new StringReader(formsText))
        {
            inventory = ReadingInventory.Parse(readings, forms, ReadingInventory.CurrentVersion);
        }

        var posHints = ReadPosHints(readingsText, inventory);

        // Prepare the rows; the feature stream is hashed as it goes.
        using var features = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var train = PrepareAll(RowPreparer.ReadRows(Path.Combine(dataRoot, TrainFile), TrainFile), inventory, adapter, features);
        var dev = PrepareAll(RowPreparer.ReadRows(Path.Combine(dataRoot, DevFile), DevFile), inventory, adapter, features);
        return new TrainingCorpus(inventory, posHints, train, dev, dataSha, features.GetHashAndReset());
    }

    /// <summary>
    ///     Decodes a table strictly.
    /// </summary>
    /// <param name="bytes">The file bytes.</param>
    /// <param name="name">The file name.</param>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidDataException">The bytes are not valid UTF-8.</exception>
    private static string Decode(byte[] bytes, string name)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException($"{name}: the file is not valid UTF-8");
        }
    }

    /// <summary>
    ///     Reads the part-of-speech hint column of the readings table.
    /// </summary>
    /// <param name="readingsText">The readings table text, already accepted by the inventory parser.</param>
    /// <param name="inventory">The inventory built from it.</param>
    /// <returns>The hints of every reading, by reading index.</returns>
    private static IReadOnlyList<IReadOnlyList<string>> ReadPosHints(string readingsText, ReadingInventory inventory)
    {
        var hints = new IReadOnlyList<string>[inventory.ReadingCount];
        Array.Fill(hints, Array.Empty<string>());
        var first = true;
        foreach (var line in readingsText.Split('\n'))
        {
            if (first)
            {
                first = false;
                continue;
            }

            var cells = line.Split('\t');
            if (cells.Length >= 3 && inventory.TryGetReadingIndex(cells[0], out var index))
            {
                hints[index] = cells[2].Split('|', StringSplitOptions.RemoveEmptyEntries);
            }
        }

        return hints;
    }

    /// <summary>
    ///     Prepares rows and feeds their features to a running hash.
    /// </summary>
    /// <param name="rows">The labeled rows.</param>
    /// <param name="inventory">The inventory.</param>
    /// <param name="adapter">The part-of-speech seam.</param>
    /// <param name="features">The running features hash.</param>
    /// <returns>The prepared rows in the same order.</returns>
    private static List<PreparedRow> PrepareAll(IReadOnlyList<LabeledRow> rows, ReadingInventory inventory, PartOfSpeechAdapter adapter, IncrementalHash features)
    {
        var prepared = new List<PreparedRow>(rows.Count);
        Span<byte> word = stackalloc byte[4];
        foreach (var row in rows)
        {
            var item = RowPreparer.Prepare(row, inventory, adapter, keepContext: false);
            prepared.Add(item);

            // Id, label and alignment, then the features of an aligned row: counts and hashes, little-endian.
            features.AppendData(Encoding.UTF8.GetBytes(row.Id + "\0" + row.ReadingId + "\0"));
            features.AppendData(item.IsAligned ? [(byte)1] : [(byte)0]);
            if (!item.IsAligned)
            {
                continue;
            }

            foreach (var group in new[] { item.Conjoined, item.Shared })
            {
                BinaryPrimitives.WriteInt32LittleEndian(word, group.Length);
                features.AppendData(word);
                foreach (var hash in group)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(word, hash);
                    features.AppendData(word);
                }
            }
        }

        return prepared;
    }

    /// <summary>
    ///     Reads the version of the part-of-speech package.
    /// </summary>
    /// <returns>The informational version without build metadata, or the assembly version when there is none.</returns>
    private static string ReadTaggerVersion()
    {
        var assembly = typeof(DemaConsulting.PartOfSpeech.Tagger).Assembly;
        var informational = (System.Reflection.AssemblyInformationalVersionAttribute?)Attribute.GetCustomAttribute(assembly, typeof(System.Reflection.AssemblyInformationalVersionAttribute));
        var version = informational?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? version[..plus] : version;
    }
}
