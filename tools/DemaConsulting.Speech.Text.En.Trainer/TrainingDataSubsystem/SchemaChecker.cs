using System.Globalization;
using System.Text.RegularExpressions;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

/// <summary>
///     The row counts of one reading, as recorded in <c>gaps-v4.tsv</c>.
/// </summary>
/// <param name="ReadingId">The reading id.</param>
/// <param name="TrainNatural">Natural (harvested) rows in <c>train.tsv</c>.</param>
/// <param name="TrainSynthetic">Synthetic rows in <c>train.tsv</c>.</param>
/// <param name="Dev">Rows in <c>dev.tsv</c>.</param>
/// <param name="HoldoutNatural">Rows in <c>holdout-natural.tsv</c>.</param>
/// <param name="HoldoutIndependent">Rows in <c>holdout-independent.tsv</c>.</param>
internal sealed record ReadingCount(
    string ReadingId,
    int TrainNatural,
    int TrainSynthetic,
    int Dev,
    int HoldoutNatural,
    int HoldoutIndependent)
{
    /// <summary>The training rows each reading should have in total.</summary>
    public const int TargetTrainTotal = 40;

    /// <summary>The natural training rows each reading should have.</summary>
    public const int TargetTrainNatural = 10;

    /// <summary>Gets the synthetic rows still missing to reach <see cref="TargetTrainTotal"/> training rows.</summary>
    public int NeedSynthetic => Math.Max(0, TargetTrainTotal - TrainNatural - TrainSynthetic);

    /// <summary>Gets the natural rows still missing to reach <see cref="TargetTrainNatural"/> natural training rows.</summary>
    public int NeedNatural => Math.Max(0, TargetTrainNatural - TrainNatural);

    /// <summary>
    ///     Renders the counts as the cells of a <c>gaps-v4.tsv</c> row.
    /// </summary>
    /// <returns>The eight cell values in column order.</returns>
    public IReadOnlyList<string> ToCells() =>
    [
        ReadingId,
        Format(TrainNatural),
        Format(TrainSynthetic),
        Format(Dev),
        Format(HoldoutNatural),
        Format(HoldoutIndependent),
        Format(NeedSynthetic),
        Format(NeedNatural),
    ];

    /// <summary>
    ///     Formats an integer with the invariant culture.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
///     The per-reading row counts of a data set.
/// </summary>
internal sealed class ReadingCounts
{
    /// <summary>Gets the counts for every reading of <c>readings.tsv</c>, in file order.</summary>
    public IReadOnlyList<ReadingCount> Items { get; }

    /// <summary>
    ///     Initializes the counts.
    /// </summary>
    /// <param name="items">The per-reading counts.</param>
    private ReadingCounts(IReadOnlyList<ReadingCount> items) => Items = items;

    /// <summary>
    ///     Counts the rows of every reading.
    /// </summary>
    /// <param name="data">The data set.</param>
    /// <returns>The counts.</returns>
    public static ReadingCounts Build(TextEnDataSet data)
    {
        ArgumentNullException.ThrowIfNull(data);

        // Count rows per reading and split, separating natural from synthetic training rows
        var trainNatural = new Dictionary<string, int>(StringComparer.Ordinal);
        var trainSynthetic = new Dictionary<string, int>(StringComparer.Ordinal);
        var dev = new Dictionary<string, int>(StringComparer.Ordinal);
        var holdoutNatural = new Dictionary<string, int>(StringComparer.Ordinal);
        var holdoutIndependent = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in data.Rows)
        {
            var target = row.Split switch
            {
                "train" => IsSynthetic(row) ? trainSynthetic : trainNatural,
                "dev" => dev,
                "holdout-natural" => holdoutNatural,
                _ => holdoutIndependent,
            };
            target[row.ReadingId] = target.GetValueOrDefault(row.ReadingId) + 1;
        }

        var items = data.Readings
            .Select(reading => new ReadingCount(
                reading.Id,
                trainNatural.GetValueOrDefault(reading.Id),
                trainSynthetic.GetValueOrDefault(reading.Id),
                dev.GetValueOrDefault(reading.Id),
                holdoutNatural.GetValueOrDefault(reading.Id),
                holdoutIndependent.GetValueOrDefault(reading.Id)))
            .ToList();
        return new ReadingCounts(items);
    }

    /// <summary>
    ///     Determines whether a row is synthetic (generated from a template) rather than natural.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns><see langword="true"/> when the source starts with <c>synthetic</c>.</returns>
    public static bool IsSynthetic(DataRow row) => row.Source.StartsWith("synthetic", StringComparison.Ordinal);

    /// <summary>
    ///     Writes the per-reading table, one tab-separated line per reading.
    /// </summary>
    /// <param name="writer">The destination writer.</param>
    public void WriteTable(TextWriter writer)
    {
        writer.WriteLine("reading_id\ttrain_natural\ttrain_synthetic\tdev\tholdout_natural\tholdout_independent\tneed_synthetic\tneed_natural");
        foreach (var item in Items)
        {
            writer.WriteLine(string.Join('\t', item.ToCells()));
        }
    }
}

/// <summary>
///     Validates the structure of the training data: the reading inventory, its mappings and forms, the labeled
///     rows with their strict spans, the provenance tables and the committed per-reading counts.
/// </summary>
/// <remarks>
///     This is the C# port of the reference <c>check_schema</c> rules. File-format defects are found earlier by
///     <see cref="TsvTable"/>; this unit checks what the cells mean. License values are judged by
///     <see cref="LicenseChecker"/>. The checks only report; they never change the data. Every violation is a
///     finding on the <c>schema</c> check with a <c>file:line</c> location where one exists.
/// </remarks>
internal sealed class SchemaChecker
{
    /// <summary>The check name used for findings.</summary>
    private const string CheckName = "schema";

    /// <summary>The only data file that may hold <c>independent-holdout</c> rows, and must hold nothing else.</summary>
    private const string IndependentHoldoutFile = "holdout-independent.tsv";

    /// <summary>The reading id grammar: <c>lemma.discriminator[.sub]</c> in lower-case ASCII.</summary>
    private static readonly Regex ReadingIdPattern = new(
        @"^[a-z0-9]+(-[a-z0-9]+)*\.[a-z0-9]+(-[a-z0-9]+)*(\.[a-z0-9]+(-[a-z0-9]+)*)?$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>Phonetic-alphabet and combining-mark ranges that must not appear in the inventory (it carries no IPA).</summary>
    private static readonly Regex IpaPattern = new(
        "[\u0250-\u02FF\u0300-\u036F\u1D00-\u1DBF]",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>The allowed part-of-speech hint tokens.</summary>
    private static readonly HashSet<string> PosTokens = new(["NOUN", "VERB", "ADJ", "ADV", "ADP", "AUX", "PROPN"], StringComparer.Ordinal);

    /// <summary>The allowed reading statuses.</summary>
    private static readonly HashSet<string> Statuses = new(["active", "deprecated"], StringComparer.Ordinal);

    /// <summary>The data set under test.</summary>
    private readonly TextEnDataSet _data;

    /// <summary>The report that receives findings.</summary>
    private readonly CheckReport _report;

    /// <summary>
    ///     Creates the checker.
    /// </summary>
    /// <param name="data">The loaded data set.</param>
    /// <param name="report">The report that receives findings and informational lines.</param>
    public SchemaChecker(TextEnDataSet data, CheckReport report)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _report = report ?? throw new ArgumentNullException(nameof(report));
    }

    /// <summary>
    ///     Runs every schema check.
    /// </summary>
    /// <returns>The per-reading row counts.</returns>
    public ReadingCounts Run()
    {
        CheckReadings();
        CheckMapping();
        CheckForms();
        CheckDecisions();
        CheckRows();
        CheckProvenance("harvest-provenance.tsv", _data.HarvestProvenance, SourceKind.Harvest);
        CheckProvenance("synthetic-provenance.tsv", _data.SyntheticProvenance, SourceKind.Synthetic);

        var counts = ReadingCounts.Build(_data);
        CheckGaps(counts);
        ReportSummary(counts);
        return counts;
    }

    /// <summary>
    ///     Checks <c>readings.tsv</c>: unique grammatical ids, lemma, tokens, status, descriptions and no IPA.
    /// </summary>
    private void CheckReadings()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var descriptions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reading in _data.Readings)
        {
            var location = $"readings.tsv:{reading.Line}";
            if (!ids.Add(reading.Id))
            {
                _report.Add(CheckName, location, $"duplicate reading id '{reading.Id}'");
            }

            if (!ReadingIdPattern.IsMatch(reading.Id))
            {
                _report.Add(CheckName, location, $"reading id '{reading.Id}' does not match lemma.discriminator[.sub] in lower-case ASCII");
            }

            if (!string.Equals(reading.Id.Split('.')[0], reading.Lemma, StringComparison.Ordinal))
            {
                _report.Add(CheckName, location, $"lemma '{reading.Lemma}' is not the prefix of '{reading.Id}'");
            }

            if (!reading.PosHint.Split('|').All(PosTokens.Contains))
            {
                _report.Add(CheckName, location, $"pos_hint '{reading.PosHint}' of '{reading.Id}' has a token outside {string.Join(", ", PosTokens)}");
            }

            if (!Statuses.Contains(reading.Status))
            {
                _report.Add(CheckName, location, $"status '{reading.Status}' of '{reading.Id}' is not active or deprecated");
            }

            if (string.IsNullOrWhiteSpace(reading.Description) || !descriptions.Add(reading.Description))
            {
                _report.Add(CheckName, location, $"description of '{reading.Id}' is empty or duplicates another description");
            }

            var cells = new[] { reading.Id, reading.Lemma, reading.PosHint, reading.Description, reading.Status };
            if (cells.Any(IpaPattern.IsMatch))
            {
                _report.Add(CheckName, location, $"'{reading.Id}' contains an IPA symbol (the inventory carries none)");
            }
        }
    }

    /// <summary>
    ///     Checks <c>whd-mapping.tsv</c>: unique word ids, existing targets and a mapping for every reading that needs one.
    /// </summary>
    private void CheckMapping()
    {
        var wordIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mapping in _data.Mapping)
        {
            var location = $"whd-mapping.tsv:{mapping.Line}";
            if (!wordIds.Add(mapping.WhdWordId))
            {
                _report.Add(CheckName, location, $"duplicate whd_wordid '{mapping.WhdWordId}'");
            }

            if (!_data.ReadingsById.ContainsKey(mapping.ReadingId))
            {
                _report.Add(CheckName, location, $"unknown reading '{mapping.ReadingId}'");
            }
        }

        // Readings that an extension adds as a new reading (not just new forms) have no evaluation-set id to map
        var extensionReadings = _data.Decisions
            .Where(decision => decision.Decision == "add" && decision.Kind != "new-forms")
            .SelectMany(decision => decision.Readings)
            .ToHashSet(StringComparer.Ordinal);
        var mapped = _data.Mapping.Select(mapping => mapping.ReadingId).ToHashSet(StringComparer.Ordinal);
        foreach (var reading in _data.Readings.Where(reading => !mapped.Contains(reading.Id) && !extensionReadings.Contains(reading.Id)))
        {
            _report.Add(CheckName, $"readings.tsv:{reading.Line}", $"reading '{reading.Id}' has no entry in whd-mapping.tsv");
        }
    }

    /// <summary>
    ///     Checks <c>forms.tsv</c>: known readings and at least one form per reading.
    /// </summary>
    private void CheckForms()
    {
        foreach (var form in _data.Forms.Where(form => !_data.ReadingsById.ContainsKey(form.ReadingId)))
        {
            _report.Add(CheckName, $"forms.tsv:{form.Line}", $"unknown reading '{form.ReadingId}'");
        }

        foreach (var reading in _data.Readings.Where(reading => _data.FormsOf(reading.Id).Count == 0))
        {
            _report.Add(CheckName, $"readings.tsv:{reading.Line}", $"reading '{reading.Id}' has no form in forms.tsv");
        }
    }

    /// <summary>
    ///     Checks <c>extension-decisions.tsv</c>: valid decisions, existing added readings and none for deferred lemmas.
    /// </summary>
    private void CheckDecisions()
    {
        foreach (var decision in _data.Decisions)
        {
            var location = $"extension-decisions.tsv:{decision.Line}";
            if (decision.Decision is not ("add" or "defer"))
            {
                _report.Add(CheckName, location, $"decision '{decision.Decision}' of '{decision.Lemma}' is not add or defer");
            }
            else if (decision.Decision == "add" && decision.Readings.Any(id => !_data.ReadingsById.ContainsKey(id)))
            {
                _report.Add(CheckName, location, $"'{decision.Lemma}' adds an unknown reading");
            }
            else if (decision.Decision == "defer" && decision.Readings.Count > 0)
            {
                _report.Add(CheckName, location, $"deferred lemma '{decision.Lemma}' must not list readings");
            }
        }
    }

    /// <summary>
    ///     Checks every labeled row: unique ids, split, reading, lemma, source and the strict span.
    /// </summary>
    private void CheckRows()
    {
        var firstFileOfId = new Dictionary<string, string>(StringComparer.Ordinal);
        var expectedSplit = TextEnDataSet.DataFiles.ToDictionary(file => file.FileName, file => file.SplitColumn, StringComparer.Ordinal);
        foreach (var row in _data.Rows)
        {
            if (!firstFileOfId.TryAdd(row.Id, row.File))
            {
                _report.Add(CheckName, row.Location, $"{row.Id}: duplicate id (also in {firstFileOfId[row.Id]})");
            }

            if (!string.Equals(row.SplitColumn, expectedSplit[row.File], StringComparison.Ordinal))
            {
                _report.Add(CheckName, row.Location, $"{row.Id}: split column '{row.SplitColumn}' (expected '{expectedSplit[row.File]}')");
            }

            CheckRowSourceConsistency(row);

            if (!_data.ReadingsById.TryGetValue(row.ReadingId, out var reading))
            {
                _report.Add(CheckName, row.Location, $"{row.Id}: unknown reading '{row.ReadingId}'");
                continue;
            }

            CheckRowDetails(row, reading);
        }
    }

    /// <summary>
    ///     Checks the lemma, source and span of a row whose reading exists.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="reading">The row's reading.</param>
    private void CheckRowDetails(DataRow row, ReadingRecord reading)
    {
        if (!string.Equals(reading.Lemma, row.Lemma, StringComparison.Ordinal))
        {
            _report.Add(CheckName, row.Location, $"{row.Id}: lemma '{row.Lemma}' is not '{reading.Lemma}', the lemma of '{row.ReadingId}'");
        }

        if (!SourceInfo.TryGet(row.Source, out _))
        {
            _report.Add(CheckName, row.Location, $"{row.Id}: unknown source '{row.Source}'");
        }

        // Offsets that are not integers were reported when the file was loaded
        if (row.Start is { } start && row.End is { } end)
        {
            var error = SpanValidator.Validate(row.Sentence, start, end, _data.FormsOf(row.ReadingId));
            if (error is not null)
            {
                _report.Add(CheckName, row.Location, $"{row.Id}: span: {error}");
            }
        }
    }

    /// <summary>
    ///     Checks that a provenance table lists exactly the ids of the rows it describes.
    /// </summary>
    /// <param name="fileName">The provenance file name.</param>
    /// <param name="records">The provenance records.</param>
    /// <param name="kind">The kind of source the table describes: synthetic rows or harvested rows.</param>
    private void CheckProvenance(string fileName, IReadOnlyList<ProvenanceRecord> records, SourceKind kind)
    {
        // What a row needs is decided by its source, never by its id: synthetic rows are described by the synthetic table,
        // harvested rows by the harvest table, and only independent holdout rows (which have no table) need nothing.
        // Rows with an unknown source are a finding of their own and are left out of the coverage.
        var described = _data.Rows.Where(row => SourceInfo.KindOf(row.Source) == kind).ToList();
        var dataIds = described.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        var unknownSourceIds = _data.Rows.Where(row => SourceInfo.KindOf(row.Source) is null).Select(row => row.Id).ToHashSet(StringComparer.Ordinal);

        var listed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            if (!listed.Add(record.Id))
            {
                _report.Add(CheckName, $"{fileName}:{record.Line}", $"duplicate provenance entry for '{record.Id}'");
            }
            else if (!dataIds.Contains(record.Id) && !unknownSourceIds.Contains(record.Id))
            {
                _report.Add(CheckName, $"{fileName}:{record.Line}", $"'{record.Id}' does not match any data row it should describe");
            }
        }

        foreach (var row in described.Where(row => !listed.Contains(row.Id)))
        {
            _report.Add(CheckName, row.Location, $"{row.Id}: no entry in {fileName}");
        }
    }

    /// <summary>
    ///     Checks that the source, the id prefix and the data file of a row agree, so no row can borrow the exemption
    ///     of the independent holdout by being renamed or moved.
    /// </summary>
    /// <param name="row">The row.</param>
    private void CheckRowSourceConsistency(DataRow row)
    {
        // An unknown source is reported with the row details; consistency is judged only for known sources
        if (SourceInfo.KindOf(row.Source) is not { } kind)
        {
            return;
        }

        var inIndependentFile = string.Equals(row.File, IndependentHoldoutFile, StringComparison.Ordinal);
        if (kind == SourceKind.IndependentHoldout && !inIndependentFile)
        {
            _report.Add(CheckName, row.Location, $"{row.Id}: source '{row.Source}' is only allowed in {IndependentHoldoutFile}");
        }
        else if (kind != SourceKind.IndependentHoldout && inIndependentFile)
        {
            _report.Add(CheckName, row.Location, $"{row.Id}: rows of {IndependentHoldoutFile} must have source '{SourceInfo.IndependentHoldoutSource}' but this one has '{row.Source}'");
        }

        var startsWithSyn = row.Id.StartsWith("syn", StringComparison.Ordinal);
        var startsWithHind = row.Id.StartsWith("hind-", StringComparison.Ordinal);
        if (kind == SourceKind.IndependentHoldout && !startsWithHind)
        {
            _report.Add(CheckName, row.Location, $"{row.Id}: id of a '{row.Source}' row must start with 'hind-'");
        }
        else if (kind == SourceKind.Synthetic && !startsWithSyn)
        {
            _report.Add(CheckName, row.Location, $"{row.Id}: id of a '{row.Source}' row must start with 'syn'");
        }
        else if (kind == SourceKind.Harvest && (startsWithSyn || startsWithHind))
        {
            _report.Add(CheckName, row.Location, $"{row.Id}: id of a '{row.Source}' row must not start with 'syn' or 'hind-'");
        }
    }

    /// <summary>
    ///     Checks that the committed <c>gaps-v4.tsv</c> equals the counts computed from the rows.
    /// </summary>
    /// <param name="counts">The computed counts.</param>
    private void CheckGaps(ReadingCounts counts)
    {
        // A duplicated reading id is its own finding; the first occurrence is the one the counts are compared with
        var expected = new Dictionary<string, ReadingCount>(StringComparer.Ordinal);
        foreach (var item in counts.Items)
        {
            expected.TryAdd(item.ReadingId, item);
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in _data.Gaps.Rows)
        {
            var location = $"gaps-v4.tsv:{row.LineNumber}";
            if (!expected.TryGetValue(row[0], out var count))
            {
                _report.Add(CheckName, location, $"'{row[0]}' is not a reading of readings.tsv");
            }
            else if (!seen.Add(row[0]))
            {
                _report.Add(CheckName, location, $"duplicate entry for '{row[0]}'");
            }
            else if (!row.Cells.SequenceEqual(count.ToCells()))
            {
                _report.Add(CheckName, location, $"'{row[0]}' is stale: expected {string.Join(' ', count.ToCells().Skip(1))} but found {string.Join(' ', row.Cells.Skip(1))}");
            }
        }

        // A reading without a row means the file was not regenerated after the inventory changed
        if (_data.Gaps.Header.SequenceEqual(TextEnDataSet.GapsHeader))
        {
            foreach (var missing in expected.Keys.Where(id => !seen.Contains(id)))
            {
                _report.Add(CheckName, "gaps-v4.tsv", $"reading '{missing}' has no row");
            }
        }
    }

    /// <summary>
    ///     Names the summary group of a row: split and kind for train and dev, the split alone for the holdouts.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The group name.</returns>
    private static string RowGroupName(DataRow row)
    {
        if (row.Split is not ("train" or "dev"))
        {
            return row.Split;
        }

        var kind = ReadingCounts.IsSynthetic(row) ? "synthetic" : "natural";
        return $"{row.Split}-{kind}";
    }

    /// <summary>
    ///     Adds the informational summary lines.
    /// </summary>
    /// <param name="counts">The computed counts.</param>
    private void ReportSummary(ReadingCounts counts)
    {
        var decisions = _data.Decisions.GroupBy(decision => decision.Decision).OrderBy(group => group.Key, StringComparer.Ordinal);
        _report.AddInfo($"extension decisions {_data.Decisions.Count} ({string.Join(", ", decisions.Select(group => $"{group.Key}: {group.Count()}"))})");
        _report.AddInfo($"readings {_data.Readings.Count} lemmas {_data.Readings.Select(reading => reading.Lemma).Distinct(StringComparer.Ordinal).Count()} " +
            $"mapped WHD ids {_data.Mapping.Count} forms {_data.DistinctFormCount}");

        // Train and dev mix natural and synthetic rows, so they are split by kind; the holdouts are counted as a whole
        var byKind = _data.Rows
            .GroupBy(RowGroupName)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}: {group.Count()}");
        _report.AddInfo($"rows: {string.Join(", ", byKind)}");

        var items = counts.Items;
        _report.AddInfo($"readings with a train row: {items.Count(item => item.TrainNatural + item.TrainSynthetic > 0)} of {items.Count}; " +
            $"with a dev row: {items.Count(item => item.Dev > 0)}; with a holdout-natural row: {items.Count(item => item.HoldoutNatural > 0)}");
        var fewIndependent = items.Where(item => item.HoldoutIndependent is > 0 and < 5).Select(item => item.ReadingId).Order(StringComparer.Ordinal);
        _report.AddInfo($"readings with a holdout-independent row: {items.Count(item => item.HoldoutIndependent > 0)}; " +
            $"with fewer than 5: [{string.Join(", ", fewIndependent)}]");
    }
}
