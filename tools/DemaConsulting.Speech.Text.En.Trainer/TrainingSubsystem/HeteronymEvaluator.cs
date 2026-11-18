using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

/// <summary>The correct and total counts of a group of rows, with the own-part-of-speech baseline for comparison.</summary>
/// <param name="Name">The group name (a source, a reading id or a confidence bucket).</param>
/// <param name="Rows">The rows of the group.</param>
/// <param name="Correct">The rows the model decides correctly.</param>
/// <param name="BaselineCorrect">The rows the own-part-of-speech baseline decides correctly.</param>
internal sealed record GroupTally(string Name, int Rows, int Correct, int BaselineCorrect);

/// <summary>The paired bootstrap interval of the accuracy difference between the model and the baseline.</summary>
/// <param name="Clusters">The number of resampling clusters.</param>
/// <param name="DifferenceBasisPoints">The point estimate of the difference (model minus baseline) in basis points, rounded half away from zero.</param>
/// <param name="LowerBasisPoints">The lower bound of the interval in basis points, rounded half away from zero.</param>
/// <param name="UpperBasisPoints">The upper bound of the interval in basis points, rounded half away from zero.</param>
/// <param name="LowerIsPositive">Whether the exact lower bound is strictly greater than zero.</param>
internal sealed record BootstrapInterval(int Clusters, int DifferenceBasisPoints, int LowerBasisPoints, int UpperBasisPoints, bool LowerIsPositive);

/// <summary>How the analyzer's end-to-end readings compare with the trainer's direct readings.</summary>
/// <param name="Compared">The aligned rows compared.</param>
/// <param name="Mismatched">The rows where both decided a reading and the two differ.</param>
/// <param name="Suppressed">The rows where the direct path decided a reading and the analyzer emitted none (the word is covered by a replacement, a pronunciation or an override).</param>
internal sealed record ParityCounts(int Compared, int Mismatched, int Suppressed);

/// <summary>The evaluation of one data set.</summary>
/// <param name="Set">The set name.</param>
/// <param name="Rows">The rows.</param>
/// <param name="Aligned">The rows whose target is exactly one runtime token.</param>
/// <param name="SingleCandidate">The rows of a one-candidate form.</param>
/// <param name="Covered">The rows for which a reading was emitted.</param>
/// <param name="Correct">The rows decided correctly end to end (a row with no reading counts wrong).</param>
/// <param name="UposBaselineCorrect">The rows the own-part-of-speech baseline decides correctly.</param>
/// <param name="MajorityBaselineCorrect">The rows the majority-reading-by-form baseline decides correctly.</param>
/// <param name="FirstBaselineCorrect">The rows the first-candidate baseline decides correctly.</param>
/// <param name="BySource">The tallies by source column, in ordinal order.</param>
/// <param name="ByReading">The tallies by labeled reading, worst accuracy first.</param>
/// <param name="ByConfidence">The tallies by reported confidence bucket.</param>
/// <param name="Interval">The paired clustered bootstrap interval of model minus own-part-of-speech baseline.</param>
/// <param name="Parity">The parity of the analyzer and the direct path; empty when only the direct path was evaluated.</param>
internal sealed record SetEvaluation(
    string Set,
    int Rows,
    int Aligned,
    int SingleCandidate,
    int Covered,
    int Correct,
    int UposBaselineCorrect,
    int MajorityBaselineCorrect,
    int FirstBaselineCorrect,
    IReadOnlyList<GroupTally> BySource,
    IReadOnlyList<GroupTally> ByReading,
    IReadOnlyList<GroupTally> ByConfidence,
    BootstrapInterval Interval,
    ParityCounts Parity);

/// <summary>One cluster of rows that a bootstrap resamples as a unit.</summary>
/// <param name="Id">The cluster id (a source document, or a row id for an independent row).</param>
/// <param name="Rows">The rows of the cluster.</param>
/// <param name="ModelCorrect">The rows the model decides correctly.</param>
/// <param name="BaselineCorrect">The rows the baseline decides correctly.</param>
internal sealed record ClusterStats(string Id, int Rows, int ModelCorrect, int BaselineCorrect);

/// <summary>The accuracy of one model of the multi-seed study.</summary>
/// <param name="Seed">The training seed.</param>
/// <param name="NaturalRows">The natural holdout rows.</param>
/// <param name="NaturalCorrect">The natural holdout rows decided correctly.</param>
/// <param name="IndependentRows">The independent holdout rows.</param>
/// <param name="IndependentCorrect">The independent holdout rows decided correctly.</param>
internal sealed record SeedResult(ulong Seed, int NaturalRows, int NaturalCorrect, int IndependentRows, int IndependentCorrect);

/// <summary>The decision of the quality gate on the natural holdout.</summary>
/// <param name="AccuracyOk">Whether the pooled accuracy reaches the threshold.</param>
/// <param name="IntervalOk">Whether the lower bound of the interval is above zero.</param>
internal sealed record GateVerdict(bool AccuracyOk, bool IntervalOk)
{
    /// <summary>Gets a value indicating whether the gate passes: both conditions hold.</summary>
    public bool Passed => AccuracyOk && IntervalOk;
}

/// <summary>
///     Measures a trained model against labeled data sets through the product's own path, with baselines, strata, a
///     paired clustered bootstrap and a parity check, all in integer arithmetic.
/// </summary>
/// <remarks>
///     <para>
///     <b>End to end.</b> A row is decided by running the real <see cref="EnglishTextAnalyzer"/> over the row's
///     sentence with the model under test and reading the reading annotation on exactly the row's span; a row for
///     which the analyzer emits no reading counts as wrong. The trainer's direct path (the runtime classifier over
///     the precomputed context) is compared with it for every aligned row; a mismatch means the features the model
///     was trained on differ from what the analyzer sees.
///     </para>
///     <para>
///     <b>Baselines.</b> The own-part-of-speech baseline picks the one candidate whose part-of-speech hint holds the
///     tagger's tag for the word, or the first candidate when there is not exactly one. The majority baseline picks
///     the reading most often labeled for the form in the training file (a tie, or a form never seen, goes to the
///     first candidate). The first-candidate baseline picks the first candidate of the form.
///     </para>
///     <para>
///     <b>Bootstrap.</b> Rows resample in clusters (the source document of a harvested row; each synthetic or
///     independently written row alone), with a seeded generator; a harvested row without a source document in
///     <c>harvest-provenance.tsv</c>, or a row of unknown source, stops the evaluation with an
///     <see cref="InvalidDataException"/> rather than counting as an independent cluster; each resample yields the exact rational difference of model and baseline correct
///     counts over rows, the resamples are ordered by exact cross-multiplication, and the interval is read at the
///     percentile indexes, so no floating-point arithmetic is involved. Not thread-safe; caches prepared sets.
///     </para>
/// </remarks>
internal sealed class HeteronymEvaluator
{
    /// <summary>The names of the evaluation sets and their files; <c>train</c> exists only for the parity check of the training rows.</summary>
    public static readonly IReadOnlyDictionary<string, string> SetFiles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["train"] = "train.tsv",
        ["dev"] = "dev.tsv",
        ["holdout-natural"] = "holdout-natural.tsv",
        ["holdout-independent"] = "holdout-independent.tsv",
    };

    /// <summary>The sets of the evaluation record and of <c>--set all</c>, in ordinal order: the development set and the holdouts.</summary>
    public static readonly IReadOnlyList<string> ReportSets = ["dev", "holdout-independent", "holdout-natural"];

    /// <summary>The confidence bucket names, in order.</summary>
    private static readonly string[] ConfidenceBuckets = ["<0.600", "0.600-0.899", ">=0.900", "none"];

    /// <summary>The corpus (inventory, hints, training rows).</summary>
    private readonly TrainingCorpus _corpus;

    /// <summary>The data folder.</summary>
    private readonly string _dataRoot;

    /// <summary>The evaluation settings.</summary>
    private readonly EvaluationSettings _settings;

    /// <summary>The majority reading of every form, by form index.</summary>
    private readonly Dictionary<int, int> _majority = [];

    /// <summary>The prepared sets by name.</summary>
    private readonly Dictionary<string, IReadOnlyList<PreparedRow>> _prepared = new(StringComparer.Ordinal);

    /// <summary>The source document of each row id in the provenance table, or null until the first harvested row needs it.</summary>
    private Dictionary<string, string>? _documents;

    /// <summary>
    ///     Initializes an evaluator.
    /// </summary>
    /// <param name="corpus">The training corpus; its training rows define the majority baseline. Must not be null.</param>
    /// <param name="dataRoot">The data folder the evaluation sets are read from. Must not be null.</param>
    /// <param name="settings">The evaluation settings. Must not be null.</param>
    public HeteronymEvaluator(TrainingCorpus corpus, string dataRoot, EvaluationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(dataRoot);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        _corpus = corpus;
        _dataRoot = dataRoot;
        _settings = settings;

        // The majority reading of each form in the training file, ties to the earlier candidate.
        var counts = new Dictionary<(int Form, int Reading), int>();
        foreach (var row in corpus.Train)
        {
            counts[(row.FormIndex, row.LabelIndex)] = counts.GetValueOrDefault((row.FormIndex, row.LabelIndex)) + 1;
        }

        foreach (var form in corpus.Train.Select(static row => row.FormIndex).Distinct())
        {
            var candidates = corpus.Inventory.GetCandidates(form).ToArray();
            var best = candidates[0];
            foreach (var candidate in candidates)
            {
                best = counts.GetValueOrDefault((form, candidate)) > counts.GetValueOrDefault((form, best)) ? candidate : best;
            }

            _majority[form] = best;
        }
    }

    /// <summary>
    ///     Decides the quality gate on an evaluation of the natural holdout.
    /// </summary>
    /// <param name="natural">The evaluation of the natural holdout.</param>
    /// <param name="settings">The settings that hold the threshold.</param>
    /// <returns>The verdict.</returns>
    public static GateVerdict DecideGate(SetEvaluation natural, EvaluationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(natural);
        ArgumentNullException.ThrowIfNull(settings);
        return new GateVerdict(
            natural.Rows > 0 && natural.Correct * 1000L >= (long)settings.GateMinPooledAccuracyPerThousand * natural.Rows,
            natural.Interval.LowerIsPositive);
    }

    /// <summary>
    ///     Evaluates a model on a set through the analyzer and checks parity with the direct path.
    /// </summary>
    /// <param name="set">The set name: <c>dev</c>, <c>holdout-natural</c> or <c>holdout-independent</c>.</param>
    /// <param name="image">The model file bytes (compressed or not).</param>
    /// <returns>The evaluation.</returns>
    /// <exception cref="InvalidDataException">The set is unknown or its file is malformed, or the model does not load.</exception>
    public SetEvaluation Evaluate(string set, byte[] image)
    {
        var rows = Prepare(set);
        var provider = new HeteronymModelProvider(() => new MemoryStream(image, writable: false), _corpus.Inventory);
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions(), PartOfSpeechAdapter.Shared, null, provider);
        var classifier = CreateClassifier(image);

        var outcomes = new List<Outcome>(rows.Count);
        var compared = 0;
        var mismatched = 0;
        var suppressed = 0;
        foreach (var row in rows)
        {
            var direct = Direct(classifier, row);
            var (reading, confidence) = Emitted(analyzer, row);
            outcomes.Add(new Outcome(row, reading, confidence));
            if (row.IsAligned)
            {
                compared++;
                if (reading >= 0 && direct is { } decided && decided.ReadingIndex != reading)
                {
                    mismatched++;
                }
                else if (reading < 0 && direct is not null)
                {
                    suppressed++;
                }
            }
        }

        return Summarize(set, outcomes, new ParityCounts(compared, mismatched, suppressed));
    }

    /// <summary>
    ///     Evaluates a model on a set with the direct path only (no analyzer), as the multi-seed study does.
    /// </summary>
    /// <param name="set">The set name.</param>
    /// <param name="image">The model file bytes.</param>
    /// <returns>The evaluation; its parity counts are zero.</returns>
    /// <exception cref="InvalidDataException">The set is unknown or its file is malformed, or the model does not load.</exception>
    public SetEvaluation EvaluateDirect(string set, byte[] image)
    {
        var rows = Prepare(set);
        var classifier = CreateClassifier(image);
        var outcomes = new List<Outcome>(rows.Count);
        foreach (var row in rows)
        {
            var direct = Direct(classifier, row);
            outcomes.Add(new Outcome(row, direct?.ReadingIndex ?? -1, direct?.Confidence ?? 0.0));
        }

        return Summarize(set, outcomes, new ParityCounts(0, 0, 0));
    }

    /// <summary>
    ///     Loads a model and builds the runtime classifier over the corpus inventory.
    /// </summary>
    /// <param name="image">The model file bytes.</param>
    /// <returns>The classifier.</returns>
    /// <exception cref="InvalidDataException">The model does not load or does not fit this build.</exception>
    private HeteronymClassifier CreateClassifier(byte[] image) => new(_corpus.Inventory, SparsePerceptronModel.Load(image));

    /// <summary>
    ///     Decides an aligned row with the runtime classifier over its precomputed context.
    /// </summary>
    /// <param name="classifier">The classifier.</param>
    /// <param name="row">The prepared row.</param>
    /// <returns>The prediction, or null for a row that is not aligned.</returns>
    private static HeteronymPrediction? Direct(HeteronymClassifier classifier, PreparedRow row) =>
        row.IsAligned ? classifier.Classify(row.Context, row.Target, row.FormIndex) : null;

    /// <summary>
    ///     Runs the analyzer over a row's sentence and reads the reading emitted on exactly the row's span.
    /// </summary>
    /// <param name="analyzer">The analyzer.</param>
    /// <param name="row">The prepared row.</param>
    /// <returns>The emitted reading index (-1 for none) and its confidence.</returns>
    private (int Reading, double Confidence) Emitted(EnglishTextAnalyzer analyzer, PreparedRow row)
    {
        var length = row.Row.End - row.Row.Start;
        var reading = -1;
        var confidence = 0.0;
        foreach (var annotation in analyzer.Analyze(row.Row.Sentence, CancellationToken.None).Annotations)
        {
            if (annotation.Start != row.Row.Start || annotation.Length != length)
            {
                continue;
            }

            if (TextEnAnnotations.TryReadReading(annotation, out var id) && _corpus.Inventory.TryGetReadingIndex(id, out var index))
            {
                reading = index;
            }
            else if (TextEnAnnotations.TryReadReadingConfidence(annotation, out var value))
            {
                confidence = value;
            }
        }

        return (reading, confidence);
    }

    /// <summary>
    ///     Prepares a set's rows once, keeping the classifier contexts.
    /// </summary>
    /// <param name="set">The set name.</param>
    /// <returns>The prepared rows in file order.</returns>
    /// <exception cref="InvalidDataException">The set is unknown or its file is malformed.</exception>
    private IReadOnlyList<PreparedRow> Prepare(string set)
    {
        if (_prepared.TryGetValue(set, out var cached))
        {
            return cached;
        }

        if (!SetFiles.TryGetValue(set, out var file))
        {
            throw new InvalidDataException($"unknown set '{set}'; use train, dev, holdout-natural or holdout-independent");
        }

        var prepared = RowPreparer.ReadRows(Path.Combine(_dataRoot, file), file)
            .Select(row => RowPreparer.Prepare(row, _corpus.Inventory, PartOfSpeechAdapter.Shared, keepContext: true))
            .ToList();
        _prepared[set] = prepared;
        return prepared;
    }

    /// <summary>
    ///     Reads the cluster id of a row: the source document of a harvested row, the row itself for a synthetic or an
    ///     independently written row. The source column decides, so a harvested row can never silently become its own cluster.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="file">The file name of the set the row belongs to, for the error message.</param>
    /// <returns>The cluster id.</returns>
    /// <exception cref="InvalidDataException">
    ///     The row's source is unknown, or the row is harvested and has no source document (the provenance table is missing,
    ///     malformed or lacks the row).
    /// </exception>
    private string ClusterOf(LabeledRow row, string file)
    {
        switch (SourceInfo.KindOf(row.Source))
        {
            case SourceKind.Synthetic:
            case SourceKind.IndependentHoldout:
                return "row:" + row.Id;

            case SourceKind.Harvest:
                var documents = LoadDocuments();
                if (documents.TryGetValue(row.Id, out var document) && document.Length > 0)
                {
                    return "doc:" + document;
                }

                throw new InvalidDataException($"{file}: harvested row '{row.Id}' has no source document in harvest-provenance.tsv; run check-data");

            default:
                throw new InvalidDataException($"{file}: row '{row.Id}' has the unknown source '{row.Source}'; run check-data");
        }
    }

    /// <summary>
    ///     Reads the provenance table once, on the first harvested row. A failed load is not cached.
    /// </summary>
    /// <returns>The source document of each row id.</returns>
    /// <exception cref="InvalidDataException">The table is missing or malformed.</exception>
    private Dictionary<string, string> LoadDocuments()
    {
        if (_documents is not null)
        {
            return _documents;
        }

        var path = Path.Combine(_dataRoot, "harvest-provenance.tsv");
        if (!File.Exists(path))
        {
            throw new InvalidDataException("harvest-provenance.tsv was not found in the data folder, but harvested rows need their source documents; run check-data");
        }

        var report = new CheckReport(10);
        var table = TsvTable.Load(path, "harvest-provenance.tsv", report, new TsvLayout(ExpectedHeader: TextEnDataSet.HarvestProvenanceHeader));
        if (report.FindingCount > 0)
        {
            throw new InvalidDataException($"harvest-provenance.tsv: {report.Findings[0].Message}");
        }

        var documents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in table.Rows)
        {
            documents[row[0]] = row[1];
        }

        _documents = documents;
        return documents;
    }

    /// <summary>
    ///     Picks the own-part-of-speech baseline reading of a row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The reading index.</returns>
    private int UposBaseline(PreparedRow row)
    {
        if (row.Upos is null)
        {
            return row.Candidates[0];
        }

        var match = -1;
        var matches = 0;
        foreach (var candidate in row.Candidates.Where(candidate => _corpus.PosHints[candidate].Contains(row.Upos, StringComparer.Ordinal)))
        {
            match = candidate;
            matches++;
        }

        return matches == 1 ? match : row.Candidates[0];
    }

    /// <summary>
    ///     Reduces the per-row outcomes to the set evaluation.
    /// </summary>
    /// <param name="set">The set name.</param>
    /// <param name="outcomes">The outcomes in file order.</param>
    /// <param name="parity">The parity counts.</param>
    /// <returns>The evaluation.</returns>
    private SetEvaluation Summarize(string set, List<Outcome> outcomes, ParityCounts parity)
    {
        var bySource = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
        var byReading = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
        var byConfidence = ConfidenceBuckets.Select(static _ => new int[3]).ToArray();
        var clusters = new Dictionary<string, int[]>(StringComparer.Ordinal);
        var clusterOrder = new List<string>();
        var aligned = 0;
        var single = 0;
        var covered = 0;
        var correct = 0;
        var upos = 0;
        var majority = 0;
        var first = 0;

        foreach (var outcome in outcomes)
        {
            var row = outcome.Row;
            var isCorrect = outcome.Reading == row.LabelIndex;
            var baseline = UposBaseline(row) == row.LabelIndex;
            aligned += row.IsAligned ? 1 : 0;
            single += row.IsAmbiguous ? 0 : 1;
            covered += outcome.Reading >= 0 ? 1 : 0;
            correct += isCorrect ? 1 : 0;
            upos += baseline ? 1 : 0;
            majority += _majority.GetValueOrDefault(row.FormIndex, row.Candidates[0]) == row.LabelIndex ? 1 : 0;
            first += row.Candidates[0] == row.LabelIndex ? 1 : 0;

            Tally(bySource, row.Row.Source, isCorrect, baseline);
            Tally(byReading, row.Row.ReadingId, isCorrect, baseline);
            var bucket = ConfidenceBucket(outcome.Reading, outcome.Confidence);
            byConfidence[bucket][0]++;
            byConfidence[bucket][1] += isCorrect ? 1 : 0;

            var cluster = ClusterOf(row.Row, SetFiles.GetValueOrDefault(set, set));
            if (!clusters.TryGetValue(cluster, out var stats))
            {
                stats = new int[3];
                clusters[cluster] = stats;
                clusterOrder.Add(cluster);
            }

            stats[0]++;
            stats[1] += isCorrect ? 1 : 0;
            stats[2] += baseline ? 1 : 0;
        }

        var interval = Bootstrap(clusterOrder.Select(id => new ClusterStats(id, clusters[id][0], clusters[id][1], clusters[id][2])).ToList(), _settings);
        var reading = byReading.Select(static pair => new GroupTally(pair.Key, pair.Value[0], pair.Value[1], pair.Value[2]))
            .OrderBy(static tally => tally, Comparer<GroupTally>.Create(static (a, b) =>
            {
                // Lowest accuracy first, compared exactly; a tie goes to the name.
                var order = (a.Correct * (long)b.Rows).CompareTo(b.Correct * (long)a.Rows);
                return order != 0 ? order : string.CompareOrdinal(a.Name, b.Name);
            }))
            .ToList();
        return new SetEvaluation(
            set,
            outcomes.Count,
            aligned,
            single,
            covered,
            correct,
            upos,
            majority,
            first,
            bySource.Select(static pair => new GroupTally(pair.Key, pair.Value[0], pair.Value[1], pair.Value[2])).ToList(),
            reading,
            ConfidenceBuckets.Select((name, index) => new GroupTally(name, byConfidence[index][0], byConfidence[index][1], 0)).ToList(),
            interval,
            parity);
    }

    /// <summary>
    ///     Picks the confidence bucket of a row.
    /// </summary>
    /// <param name="reading">The emitted reading index, or -1 for none.</param>
    /// <param name="confidence">The reported confidence.</param>
    /// <returns>The index into the confidence bucket names.</returns>
    private static int ConfidenceBucket(int reading, double confidence)
    {
        if (reading < 0)
        {
            return 3;
        }

        var thousandths = (int)Math.Round(confidence * 1000.0, MidpointRounding.AwayFromZero);
        if (thousandths < 600)
        {
            return 0;
        }

        return thousandths < 900 ? 1 : 2;
    }

    /// <summary>
    ///     Adds one row to a tally dictionary.
    /// </summary>
    /// <param name="tallies">The tallies by name.</param>
    /// <param name="name">The group name.</param>
    /// <param name="correct">Whether the model is right.</param>
    /// <param name="baseline">Whether the baseline is right.</param>
    private static void Tally(SortedDictionary<string, int[]> tallies, string name, bool correct, bool baseline)
    {
        if (!tallies.TryGetValue(name, out var tally))
        {
            tally = new int[3];
            tallies[name] = tally;
        }

        tally[0]++;
        tally[1] += correct ? 1 : 0;
        tally[2] += baseline ? 1 : 0;
    }

    /// <summary>
    ///     Computes the paired clustered bootstrap interval of the accuracy difference.
    /// </summary>
    /// <param name="clusters">The clusters. May be empty.</param>
    /// <param name="settings">The settings (resamples, confidence, seed).</param>
    /// <returns>The interval; all zero for no clusters.</returns>
    /// <remarks>
    ///     Each resample draws as many clusters as there are, with replacement, and yields the integer pair of the
    ///     summed correct-count difference and the summed rows. The pairs are ordered by exact cross-multiplication
    ///     and the interval is read at the indexes <c>R (100 - confidence) / 200</c> and <c>R - 1</c> minus that.
    /// </remarks>
    public static BootstrapInterval Bootstrap(IReadOnlyList<ClusterStats> clusters, EvaluationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(clusters);
        ArgumentNullException.ThrowIfNull(settings);
        if (clusters.Count == 0)
        {
            return new BootstrapInterval(0, 0, 0, 0, false);
        }

        long totalRows = 0;
        long totalDifference = 0;
        foreach (var cluster in clusters)
        {
            totalRows += cluster.Rows;
            totalDifference += cluster.ModelCorrect - cluster.BaselineCorrect;
        }

        var random = new DeterministicRandom(settings.BootstrapSeed);
        var resamples = new (long Difference, long Rows)[settings.Resamples];
        for (var r = 0; r < resamples.Length; r++)
        {
            long difference = 0;
            long rows = 0;
            for (var i = 0; i < clusters.Count; i++)
            {
                var cluster = clusters[random.NextInt(clusters.Count)];
                difference += cluster.ModelCorrect - cluster.BaselineCorrect;
                rows += cluster.Rows;
            }

            resamples[r] = (difference, rows);
        }

        Array.Sort(resamples, static (a, b) => (a.Difference * b.Rows).CompareTo(b.Difference * a.Rows));
        var lowIndex = (int)((long)settings.Resamples * (100 - settings.ConfidencePercent) / 200);
        var highIndex = settings.Resamples - 1 - lowIndex;
        var low = resamples[lowIndex];
        var high = resamples[highIndex];
        return new BootstrapInterval(
            clusters.Count,
            RoundBasisPoints(totalDifference, totalRows),
            RoundBasisPoints(low.Difference, low.Rows),
            RoundBasisPoints(high.Difference, high.Rows),
            low.Difference > 0);
    }

    /// <summary>
    ///     Converts a ratio to basis points, rounded half away from zero, with integers only.
    /// </summary>
    /// <param name="numerator">The numerator; may be negative.</param>
    /// <param name="denominator">The denominator; positive.</param>
    /// <returns>The ratio times 10000, rounded.</returns>
    public static int RoundBasisPoints(long numerator, long denominator)
    {
        if (denominator <= 0)
        {
            return 0;
        }

        var magnitude = ((2 * Math.Abs(numerator) * 10_000) + denominator) / (2 * denominator);
        return (int)(numerator < 0 ? -magnitude : magnitude);
    }

    /// <summary>One row's end-to-end result.</summary>
    /// <param name="Row">The prepared row.</param>
    /// <param name="Reading">The emitted reading index, or -1.</param>
    /// <param name="Confidence">The reported confidence; 0 when none.</param>
    private sealed record Outcome(PreparedRow Row, int Reading, double Confidence);
}
