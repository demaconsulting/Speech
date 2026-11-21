using System.Globalization;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

/// <summary>
///     The license identifiers the data may carry.
/// </summary>
internal static class LicenseIds
{
    /// <summary>Creative Commons Zero 1.0: generated and LLM-labeled content dedicated to the public domain.</summary>
    public const string Cc0 = "CC0-1.0";

    /// <summary>Public-domain works (for example US federal government works).</summary>
    public const string PublicDomain = "Public-Domain";

    /// <summary>Creative Commons Attribution 3.0 United States (the MASC corpus), which requires attribution.</summary>
    public const string CcBy30Us = "CC-BY-3.0-US";

    /// <summary>The complete allow-list.</summary>
    public static readonly IReadOnlySet<string> Allowed =
        new HashSet<string>([Cc0, PublicDomain, CcBy30Us], StringComparer.Ordinal);
}

/// <summary>
///     The kind of a source: it decides which provenance table must describe a row.
/// </summary>
internal enum SourceKind
{
    /// <summary>A sentence harvested from a corpus (<c>harvest-*</c>); described by <c>harvest-provenance.tsv</c>.</summary>
    Harvest,

    /// <summary>A sentence generated from a template (<c>synthetic*</c>); described by <c>synthetic-provenance.tsv</c>.</summary>
    Synthetic,

    /// <summary>An independently written holdout sentence; it has no provenance table and lives only in <c>holdout-independent.tsv</c>.</summary>
    IndependentHoldout,
}

/// <summary>
///     What the closed source vocabulary says about one value of the <c>source</c> column.
/// </summary>
/// <param name="Name">The source value, for example <c>harvest-masc-llm</c>.</param>
/// <param name="IsGenerated">Whether rows of this source are generated content (synthetic or independent holdout) rather than harvested sentences.</param>
/// <param name="AllowedLicenses">The licenses rows of this source may carry.</param>
internal sealed record SourceInfo(string Name, bool IsGenerated, IReadOnlySet<string> AllowedLicenses)
{
    /// <summary>The source of the independently written holdout rows, which have no provenance table.</summary>
    public const string IndependentHoldoutSource = "independent-holdout";

    /// <summary>The harvest corpora of the dataset; each also exists with an <c>-llm</c> suffix for LLM-labeled rows.</summary>
    private static readonly string[] HarvestCorpora =
    [
        "masc", "open-train", "imperative", "compound", "compound-2", "tech-deverbal", "tech-terms", "tech-steps",
        "tech-clauses", "headings", "headings-2", "silver",
    ];

    /// <summary>The generated sources, which are always CC0-1.0.</summary>
    private static readonly string[] GeneratedSources = ["synthetic", "synthetic-v2", "synthetic-v3", IndependentHoldoutSource];

    /// <summary>The vocabulary keyed by source value.</summary>
    private static readonly Dictionary<string, SourceInfo> Vocabulary = BuildVocabulary();

    /// <summary>
    ///     Looks a source value up in the closed vocabulary.
    /// </summary>
    /// <param name="source">The value of the <c>source</c> column.</param>
    /// <param name="info">The vocabulary entry when found.</param>
    /// <returns><see langword="true"/> when the source is part of the vocabulary.</returns>
    public static bool TryGet(string source, out SourceInfo info)
    {
        // The out parameter is never null when the lookup succeeds; the default is only seen on failure
        var found = Vocabulary.TryGetValue(source, out var value);
        info = value!;
        return found;
    }

    /// <summary>
    ///     Gets the kind of a source value; every provenance and separation rule keys on this, never on a row id.
    /// </summary>
    /// <param name="source">The value of the <c>source</c> column.</param>
    /// <returns>The kind, or <see langword="null"/> when the source is not part of the closed vocabulary.</returns>
    public static SourceKind? KindOf(string source)
    {
        if (!TryGet(source, out var info))
        {
            return null;
        }

        if (!info.IsGenerated)
        {
            return SourceKind.Harvest;
        }

        return string.Equals(info.Name, IndependentHoldoutSource, StringComparison.Ordinal)
            ? SourceKind.IndependentHoldout
            : SourceKind.Synthetic;
    }

    /// <summary>
    ///     Builds the vocabulary: generated sources are CC0, MASC is CC-BY, open-train and silver are CC0 or
    ///     public domain, every other corpus is public domain.
    /// </summary>
    /// <returns>The vocabulary.</returns>
    private static Dictionary<string, SourceInfo> BuildVocabulary()
    {
        var result = new Dictionary<string, SourceInfo>(StringComparer.Ordinal);
        foreach (var name in GeneratedSources)
        {
            result[name] = new SourceInfo(name, true, new HashSet<string>([LicenseIds.Cc0], StringComparer.Ordinal));
        }

        foreach (var corpus in HarvestCorpora)
        {
            var licenses = corpus switch
            {
                "masc" => new[] { LicenseIds.CcBy30Us },
                "open-train" or "silver" => [LicenseIds.Cc0, LicenseIds.PublicDomain],
                _ => [LicenseIds.PublicDomain],
            };

            // Both the labeled-by-annotation and the LLM-labeled variant of a corpus carry the corpus license
            foreach (var name in new[] { "harvest-" + corpus, "harvest-" + corpus + "-llm" })
            {
                result[name] = new SourceInfo(name, false, new HashSet<string>(licenses, StringComparer.Ordinal));
            }
        }

        return result;
    }
}

/// <summary>One row of <c>readings.tsv</c>: a pronunciation reading of a lemma.</summary>
/// <param name="Line">The 1-based line.</param>
/// <param name="Id">The reading id, for example <c>lead.verb</c>.</param>
/// <param name="Lemma">The lemma the reading belongs to.</param>
/// <param name="PosHint">The pipe-separated part-of-speech hint tokens.</param>
/// <param name="Description">The description distinguishing the reading.</param>
/// <param name="Status">Either <c>active</c> or <c>deprecated</c>.</param>
internal sealed record ReadingRecord(int Line, string Id, string Lemma, string PosHint, string Description, string Status);

/// <summary>One row of <c>whd-mapping.tsv</c>: an id-to-id mapping from an evaluation-set word id to a reading.</summary>
/// <param name="Line">The 1-based line.</param>
/// <param name="WhdWordId">The evaluation-set word id.</param>
/// <param name="ReadingId">The reading id it maps to.</param>
internal sealed record WhdMappingRecord(int Line, string WhdWordId, string ReadingId);

/// <summary>One row of <c>forms.tsv</c>: a surface form a reading may have.</summary>
/// <param name="Line">The 1-based line.</param>
/// <param name="Form">The lower-case surface form.</param>
/// <param name="ReadingId">The reading id.</param>
/// <param name="Kind">The origin of the form.</param>
internal sealed record FormRecord(int Line, string Form, string ReadingId, string Kind);

/// <summary>One row of <c>extension-decisions.tsv</c>: the decision about adding a lemma.</summary>
/// <param name="Line">The 1-based line.</param>
/// <param name="Lemma">The lemma.</param>
/// <param name="Kind">The kind of extension, for example <c>new-forms</c>.</param>
/// <param name="Decision">Either <c>add</c> or <c>defer</c>.</param>
/// <param name="Readings">The reading ids the decision adds (empty for <c>defer</c>).</param>
internal sealed record ExtensionDecision(int Line, string Lemma, string Kind, string Decision, IReadOnlyList<string> Readings);

/// <summary>One row of a provenance table, reduced to what the checks need.</summary>
/// <param name="Line">The 1-based line.</param>
/// <param name="Id">The data row id the provenance describes.</param>
/// <param name="Key">The source document id (harvest provenance) or the template (synthetic provenance).</param>
internal sealed record ProvenanceRecord(int Line, string Id, string Key);

/// <summary>Describes one of the four labeled data files.</summary>
/// <param name="FileName">The file name.</param>
/// <param name="Split">The logical split name, used in reports.</param>
/// <param name="SplitColumn">The value the <c>split</c> column must hold in this file.</param>
internal sealed record DataFileInfo(string FileName, string Split, string SplitColumn);

/// <summary>One labeled example of a data file.</summary>
/// <param name="File">The file name the row came from.</param>
/// <param name="Split">The logical split name of the file.</param>
/// <param name="Line">The 1-based line.</param>
/// <param name="Id">The row id.</param>
/// <param name="Lemma">The lemma column.</param>
/// <param name="ReadingId">The reading id column.</param>
/// <param name="Sentence">The sentence text (no quoting is applied).</param>
/// <param name="Start">The first UTF-16 code unit of the target word, or <see langword="null"/> when not an integer.</param>
/// <param name="End">The UTF-16 code unit after the target word, or <see langword="null"/> when not an integer.</param>
/// <param name="Source">The source column.</param>
/// <param name="License">The license column.</param>
/// <param name="SplitColumn">The split column as written in the file.</param>
internal sealed record DataRow(
    string File,
    string Split,
    int Line,
    string Id,
    string Lemma,
    string ReadingId,
    string Sentence,
    int? Start,
    int? End,
    string Source,
    string License,
    string SplitColumn)
{
    /// <summary>Gets the location text <c>file:line</c> used in findings.</summary>
    public string Location => $"{File}:{Line}";
}

/// <summary>One row of <c>regression.tsv</c>: an evaluation overlay row that refers to an existing example.</summary>
/// <param name="File">The display name of the file the row came from.</param>
/// <param name="Line">The 1-based line.</param>
/// <param name="Id">The row id, <c>reg-NNNN</c>.</param>
/// <param name="Lemma">The lemma column.</param>
/// <param name="ReadingId">The reading id column.</param>
/// <param name="Sentence">The sentence text.</param>
/// <param name="Start">The first UTF-16 code unit of the target word, or <see langword="null"/> when not an integer.</param>
/// <param name="End">The UTF-16 code unit after the target word, or <see langword="null"/> when not an integer.</param>
/// <param name="Origin">Where the row comes from, for example <c>listening</c> or <c>data-row</c>.</param>
/// <param name="Reference">The reference to the original, for example <c>train.tsv:syn-10500</c>.</param>
/// <param name="SeenInTraining">Either <c>yes</c> or <c>no</c>, as written in the file.</param>
/// <param name="License">The license column.</param>
internal sealed record RegressionRecord(
    string File,
    int Line,
    string Id,
    string Lemma,
    string ReadingId,
    string Sentence,
    int? Start,
    int? End,
    string Origin,
    string Reference,
    string SeenInTraining,
    string License)
{
    /// <summary>Gets the location text <c>file:line</c> used in findings.</summary>
    public string Location => $"{File}:{Line}";
}

/// <summary>One row of <c>generalization.tsv</c>: a row of the blind generalization test set.</summary>
/// <param name="File">The display name of the file the row came from.</param>
/// <param name="Line">The 1-based line.</param>
/// <param name="Id">The row id, <c>gen-NNNN</c>.</param>
/// <param name="Lemma">The lemma column.</param>
/// <param name="ReadingId">The reading id column.</param>
/// <param name="Sentence">The sentence text.</param>
/// <param name="Start">The first UTF-16 code unit of the target word, or <see langword="null"/> when not an integer.</param>
/// <param name="End">The UTF-16 code unit after the target word, or <see langword="null"/> when not an integer.</param>
/// <param name="Kind">Either <c>novel-vocabulary</c> or <c>novel-construction</c>.</param>
/// <param name="Family">The cue or template family, <c>{lemma}:{cue-type}</c>; the bootstrap cluster of the row.</param>
/// <param name="CueForms">The pipe-separated designated cue forms, or <c>-</c>.</param>
/// <param name="CueUnsolvable">Either <c>yes</c> or <c>no</c>, as written in the file.</param>
/// <param name="Source">The source column, always <c>generalization</c>.</param>
/// <param name="License">The license column, always <c>CC0-1.0</c>.</param>
internal sealed record GeneralizationRecord(
    string File,
    int Line,
    string Id,
    string Lemma,
    string ReadingId,
    string Sentence,
    int? Start,
    int? End,
    string Kind,
    string Family,
    string CueForms,
    string CueUnsolvable,
    string Source,
    string License)
{
    /// <summary>Gets the location text <c>file:line</c> used in findings.</summary>
    public string Location => $"{File}:{Line}";
}

/// <summary>
///     The typed contents of the Text.En training data folder, loaded once and shared by all checks.
/// </summary>
/// <remarks>
///     Loading never throws for bad data: a missing file, a wrong header, a format defect or a non-integer offset
///     becomes a finding and the affected rows are simply absent, so every check can run on whatever is loadable.
///     The dataset is a tool input, not a software item; this class is its in-memory view. Files are read with
///     <see cref="TsvTable"/> (tab-split, no quoting). Immutable after loading.
/// </remarks>
internal sealed class TextEnDataSet
{
    /// <summary>The header of <c>readings.tsv</c>.</summary>
    public static readonly IReadOnlyList<string> ReadingsHeader = ["reading_id", "lemma", "pos_hint", "description", "status"];

    /// <summary>The header of <c>whd-mapping.tsv</c>.</summary>
    public static readonly IReadOnlyList<string> MappingHeader = ["whd_wordid", "reading_id"];

    /// <summary>The header of <c>forms.tsv</c>.</summary>
    public static readonly IReadOnlyList<string> FormsHeader = ["form", "reading_id", "kind"];

    /// <summary>The header of <c>extension-decisions.tsv</c>.</summary>
    public static readonly IReadOnlyList<string> DecisionsHeader =
    [
        "rank", "lemma", "forms", "kind", "class", "decision", "reason", "cmudict_evidence", "readings", "readings_added",
    ];

    /// <summary>The header of the four labeled data files.</summary>
    public static readonly IReadOnlyList<string> RowsHeader =
        ["id", "lemma", "reading_id", "sentence", "start", "end", "source", "license", "split"];

    /// <summary>The header of <c>harvest-provenance.tsv</c>.</summary>
    public static readonly IReadOnlyList<string> HarvestProvenanceHeader = ["id", "source_id", "corpus", "sent_id", "label_method"];

    /// <summary>The header of <c>synthetic-provenance.tsv</c>.</summary>
    public static readonly IReadOnlyList<string> SyntheticProvenanceHeader = ["id", "spike_id", "batch", "template"];

    /// <summary>The header of <c>gaps-v4.tsv</c>.</summary>
    public static readonly IReadOnlyList<string> GapsHeader =
    [
        "reading_id", "train_natural", "train_synthetic", "dev", "holdout_natural", "holdout_independent", "need_synthetic",
        "need_natural",
    ];

    /// <summary>The four labeled data files in the order the checks read them.</summary>
    public static readonly IReadOnlyList<DataFileInfo> DataFiles =
    [
        new("train.tsv", "train", "train"),
        new("dev.tsv", "dev", "dev"),
        new("holdout-natural.tsv", "holdout-natural", "holdout-natural"),
        new("holdout-independent.tsv", "holdout-independent", "test"),
    ];

    /// <summary>The file name of the regression overlay.</summary>
    public const string RegressionFile = "regression.tsv";

    /// <summary>The file name of the generalization set.</summary>
    public const string GeneralizationFile = "generalization.tsv";

    /// <summary>The header of <c>regression.tsv</c>.</summary>
    public static readonly IReadOnlyList<string> RegressionHeader =
        ["id", "lemma", "reading_id", "sentence", "start", "end", "origin", "ref", "seen_in_training", "license"];

    /// <summary>The header of <c>generalization.tsv</c>.</summary>
    public static readonly IReadOnlyList<string> GeneralizationHeader =
        ["id", "lemma", "reading_id", "sentence", "start", "end", "kind", "family", "cue_forms", "cue_unsolvable", "source", "license"];

    /// <summary>The check name for load-time findings that concern the content of a file.</summary>
    private const string SchemaCheck = "schema";

    /// <summary>The forms of a reading that has none.</summary>
    private static readonly IReadOnlySet<string> NoForms = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The forms per reading id.</summary>
    private readonly Dictionary<string, HashSet<string>> _formsOfReading = new(StringComparer.Ordinal);

    /// <summary>Gets the data root folder.</summary>
    public string Root { get; }

    /// <summary>Gets the rows of <c>readings.tsv</c>.</summary>
    public IReadOnlyList<ReadingRecord> Readings { get; private init; } = [];

    /// <summary>Gets the first reading record for each reading id.</summary>
    public IReadOnlyDictionary<string, ReadingRecord> ReadingsById { get; private init; } = new Dictionary<string, ReadingRecord>();

    /// <summary>Gets the rows of <c>whd-mapping.tsv</c>.</summary>
    public IReadOnlyList<WhdMappingRecord> Mapping { get; private init; } = [];

    /// <summary>Gets the rows of <c>forms.tsv</c>.</summary>
    public IReadOnlyList<FormRecord> Forms { get; private init; } = [];

    /// <summary>Gets the number of distinct form strings in <c>forms.tsv</c>.</summary>
    public int DistinctFormCount { get; private init; }

    /// <summary>Gets the rows of <c>extension-decisions.tsv</c>.</summary>
    public IReadOnlyList<ExtensionDecision> Decisions { get; private init; } = [];

    /// <summary>Gets the rows of the four labeled data files, in file order (train, dev, holdout-natural, holdout-independent).</summary>
    public IReadOnlyList<DataRow> Rows { get; private init; } = [];

    /// <summary>Gets the rows of <c>harvest-provenance.tsv</c> (<see cref="ProvenanceRecord.Key"/> is the source document id).</summary>
    public IReadOnlyList<ProvenanceRecord> HarvestProvenance { get; private init; } = [];

    /// <summary>Gets the rows of <c>synthetic-provenance.tsv</c> (<see cref="ProvenanceRecord.Key"/> is the template).</summary>
    public IReadOnlyList<ProvenanceRecord> SyntheticProvenance { get; private init; } = [];

    /// <summary>Gets the table of <c>gaps-v4.tsv</c>.</summary>
    public TsvTable Gaps { get; private init; } = null!;

    /// <summary>Gets the rows of the regression overlay <c>regression.tsv</c>, in file order.</summary>
    public IReadOnlyList<RegressionRecord> Regression { get; private init; } = [];

    /// <summary>Gets the rows of the generalization set <c>generalization.tsv</c>, in file order (empty until the set is written).</summary>
    public IReadOnlyList<GeneralizationRecord> Generalization { get; private init; } = [];

    /// <summary>
    ///     Initializes an empty data set for a root folder.
    /// </summary>
    /// <param name="root">The data root folder.</param>
    private TextEnDataSet(string root) => Root = root;

    /// <summary>
    ///     Loads the data folder, adding findings for every file that cannot be loaded cleanly.
    /// </summary>
    /// <param name="root">The data root folder.</param>
    /// <param name="report">The report that receives the findings.</param>
    /// <returns>The loaded data set.</returns>
    public static TextEnDataSet Load(string root, CheckReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var readings = LoadReadings(root, report);
        var mapping = Load(root, "whd-mapping.tsv", MappingHeader, report)
            .Rows.Select(row => new WhdMappingRecord(row.LineNumber, row[0], row[1])).ToList();
        var forms = Load(root, "forms.tsv", FormsHeader, report)
            .Rows.Select(row => new FormRecord(row.LineNumber, row[0], row[1], row[2])).ToList();
        var decisions = Load(root, "extension-decisions.tsv", DecisionsHeader, report)
            .Rows.Select(row => new ExtensionDecision(
                row.LineNumber,
                row[1],
                row[3],
                row[5],
                row[8].Split(';', StringSplitOptions.RemoveEmptyEntries))).ToList();

        var dataSet = new TextEnDataSet(root)
        {
            Readings = readings,
            ReadingsById = IndexFirst(readings),
            Mapping = mapping,
            Forms = forms,
            DistinctFormCount = forms.Select(form => form.Form).Distinct(StringComparer.Ordinal).Count(),
            Decisions = decisions,
            Rows = LoadRows(root, report),
            HarvestProvenance = LoadProvenance(root, "harvest-provenance.tsv", HarvestProvenanceHeader, 1, report),
            SyntheticProvenance = LoadProvenance(root, "synthetic-provenance.tsv", SyntheticProvenanceHeader, 3, report),
            Gaps = Load(root, "gaps-v4.tsv", GapsHeader, report),
            Regression = LoadRegression(Path.Combine(root, RegressionFile), RegressionFile, report),
            Generalization = LoadGeneralization(Path.Combine(root, GeneralizationFile), GeneralizationFile, report),
        };

        // Index the forms by reading so a span check is a single set lookup
        foreach (var form in forms)
        {
            if (!dataSet._formsOfReading.TryGetValue(form.ReadingId, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                dataSet._formsOfReading[form.ReadingId] = set;
            }

            set.Add(form.Form);
        }

        CheckFormatOfOtherTables(root, report);
        return dataSet;
    }

    /// <summary>
    ///     Applies the strict file-format rules to every other TSV file of the data folder (audit tables, exclusions and so on).
    /// </summary>
    /// <param name="root">The data root folder.</param>
    /// <param name="report">The report that receives findings.</param>
    private static void CheckFormatOfOtherTables(string root, CheckReport report)
    {
        // The typed tables above already applied the same rules, so only the remaining files are loaded here
        var typed = new HashSet<string>(StringComparer.Ordinal)
        {
            "readings.tsv", "whd-mapping.tsv", "forms.tsv", "extension-decisions.tsv", "harvest-provenance.tsv",
            "synthetic-provenance.tsv", "gaps-v4.tsv", RegressionFile, GeneralizationFile,
        };
        foreach (var file in DataFiles)
        {
            typed.Add(file.FileName);
        }

        // The corpus tables are loaded with their exact headers by the corpus check
        typed.UnionWith(CorpusDataChecker.Tables);

        foreach (var relative in DataManifest.EnumerateDataFiles(root).Where(file => file.EndsWith(".tsv", StringComparison.Ordinal) && !typed.Contains(file)))
        {
            TsvTable.Load(Path.Combine(root, relative), relative, report);
        }
    }

    /// <summary>
    ///     Gets the forms <c>forms.tsv</c> lists for a reading.
    /// </summary>
    /// <param name="readingId">The reading id.</param>
    /// <returns>The set of forms; empty for an unknown reading or one without forms.</returns>
    public IReadOnlySet<string> FormsOf(string readingId) =>
        _formsOfReading.TryGetValue(readingId, out var forms) ? forms : NoForms;

    /// <summary>
    ///     Loads one table of the data folder with its exact header.
    /// </summary>
    /// <param name="root">The data root folder.</param>
    /// <param name="relativePath">The path relative to the root.</param>
    /// <param name="header">The expected header.</param>
    /// <param name="report">The report that receives findings.</param>
    /// <returns>The table.</returns>
    private static TsvTable Load(string root, string relativePath, IReadOnlyList<string> header, CheckReport report) =>
        TsvTable.Load(Path.Combine(root, relativePath), relativePath, report, new TsvLayout(ExpectedHeader: header));

    /// <summary>
    ///     Loads <c>readings.tsv</c>.
    /// </summary>
    /// <param name="root">The data root folder.</param>
    /// <param name="report">The report that receives findings.</param>
    /// <returns>The readings in file order.</returns>
    private static List<ReadingRecord> LoadReadings(string root, CheckReport report) =>
        Load(root, "readings.tsv", ReadingsHeader, report).Rows
            .Select(row => new ReadingRecord(row.LineNumber, row[0], row[1], row[2], row[3], row[4]))
            .ToList();

    /// <summary>
    ///     Indexes readings by id, keeping the first of any duplicates (duplicates are a schema finding).
    /// </summary>
    /// <param name="readings">The readings.</param>
    /// <returns>The index.</returns>
    private static Dictionary<string, ReadingRecord> IndexFirst(IEnumerable<ReadingRecord> readings)
    {
        var index = new Dictionary<string, ReadingRecord>(StringComparer.Ordinal);
        foreach (var reading in readings)
        {
            index.TryAdd(reading.Id, reading);
        }

        return index;
    }

    /// <summary>
    ///     Loads a provenance table.
    /// </summary>
    /// <param name="root">The data root folder.</param>
    /// <param name="relativePath">The file name.</param>
    /// <param name="header">The expected header.</param>
    /// <param name="keyColumn">The column holding the key (source document id or template).</param>
    /// <param name="report">The report that receives findings.</param>
    /// <returns>The provenance records.</returns>
    private static List<ProvenanceRecord> LoadProvenance(string root, string relativePath, IReadOnlyList<string> header, int keyColumn, CheckReport report) =>
        Load(root, relativePath, header, report).Rows
            .Select(row => new ProvenanceRecord(row.LineNumber, row[0], row[keyColumn]))
            .ToList();

    /// <summary>
    ///     Loads the four labeled data files.
    /// </summary>
    /// <param name="root">The data root folder.</param>
    /// <param name="report">The report that receives findings.</param>
    /// <returns>All rows in file order.</returns>
    private static List<DataRow> LoadRows(string root, CheckReport report)
    {
        var rows = new List<DataRow>();
        foreach (var file in DataFiles)
        {
            foreach (var row in Load(root, file.FileName, RowsHeader, report).Rows)
            {
                var start = ParseOffset(row[4]);
                var end = ParseOffset(row[5]);
                if (start is null || end is null)
                {
                    report.Add(SchemaCheck, $"{file.FileName}:{row.LineNumber}", $"{row[0]}: start and end must be integers");
                }

                rows.Add(new DataRow(
                    file.FileName, file.Split, row.LineNumber, row[0], row[1], row[2], row[3], start, end, row[6], row[7], row[8]));
            }
        }

        return rows;
    }

    /// <summary>
    ///     Loads a regression overlay file.
    /// </summary>
    /// <remarks>
    ///     A missing file, a wrong header or a format defect is a finding and yields no rows; a non-integer offset is a
    ///     finding and leaves the offset <see langword="null"/>. Never throws for bad data.
    /// </remarks>
    /// <param name="path">The full path of the file.</param>
    /// <param name="name">The name shown in findings and carried by every record.</param>
    /// <param name="report">The report that receives findings.</param>
    /// <returns>The rows in file order.</returns>
    public static IReadOnlyList<RegressionRecord> LoadRegression(string path, string name, CheckReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return TsvTable.Load(path, name, report, new TsvLayout(ExpectedHeader: RegressionHeader)).Rows
            .Select(row => new RegressionRecord(
                name,
                row.LineNumber,
                row[0],
                row[1],
                row[2],
                row[3],
                ParseOffset(row[4], name, row, report),
                ParseOffset(row[5], name, row, report),
                row[6],
                row[7],
                row[8],
                row[9]))
            .ToList();
    }

    /// <summary>
    ///     Loads a generalization set file: the committed <c>generalization.tsv</c> or a staged copy outside the repository.
    /// </summary>
    /// <remarks>
    ///     A missing file, a wrong header or a format defect is a finding and yields no rows; a non-integer offset is a
    ///     finding and leaves the offset <see langword="null"/>. A file with the header only is valid and has no rows.
    ///     Never throws for bad data.
    /// </remarks>
    /// <param name="path">The full path of the file.</param>
    /// <param name="name">The name shown in findings and carried by every record.</param>
    /// <param name="report">The report that receives findings.</param>
    /// <returns>The rows in file order.</returns>
    public static IReadOnlyList<GeneralizationRecord> LoadGeneralization(string path, string name, CheckReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return TsvTable.Load(path, name, report, new TsvLayout(ExpectedHeader: GeneralizationHeader)).Rows
            .Select(row => new GeneralizationRecord(
                name,
                row.LineNumber,
                row[0],
                row[1],
                row[2],
                row[3],
                ParseOffset(row[4], name, row, report),
                ParseOffset(row[5], name, row, report),
                row[6],
                row[7],
                row[8],
                row[9],
                row[10],
                row[11]))
            .ToList();
    }

    /// <summary>
    ///     Parses an offset cell of an overlay row, reporting a cell that is not an integer.
    /// </summary>
    /// <param name="text">The cell text.</param>
    /// <param name="name">The file display name.</param>
    /// <param name="row">The row, whose first cell is its id.</param>
    /// <param name="report">The report that receives findings.</param>
    /// <returns>The integer, or <see langword="null"/> when the cell is not an integer.</returns>
    private static int? ParseOffset(string text, string name, TsvRow row, CheckReport report)
    {
        var value = ParseOffset(text);
        if (value is null)
        {
            report.Add(SchemaCheck, $"{name}:{row.LineNumber}", $"{row[0]}: start and end must be integers");
        }

        return value;
    }

    /// <summary>
    ///     Parses an offset cell.
    /// </summary>
    /// <param name="text">The cell text.</param>
    /// <returns>The integer, or <see langword="null"/> when the cell is not an integer.</returns>
    private static int? ParseOffset(string text) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : null;
}
