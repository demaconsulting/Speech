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
/// <param name="AccuracyInterval">The clustered bootstrap interval of the model accuracy itself (its point estimate is the accuracy in basis points); null when not requested.</param>
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
    ParityCounts Parity,
    BootstrapInterval? AccuracyInterval = null);

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

/// <summary>The result of one overlay row with and without the curated cue priors.</summary>
/// <param name="Overlay">The overlay row.</param>
/// <param name="Shipped">The reading the shipped classifier (with its cue priors) emits end to end, or <c>none</c>.</param>
/// <param name="ModelOnly">The reading the model alone (without the cue priors) chooses on the same path, or <c>none</c>.</param>
/// <param name="CueFired">Whether a cue prior is added to at least one candidate of the row.</param>
internal sealed record OverlayRowOutcome(OverlayRow Overlay, string Shipped, string ModelOnly, bool CueFired)
{
    /// <summary>Gets a value indicating whether the shipped reading is the labeled one.</summary>
    public bool ShippedCorrect => Shipped == Overlay.Row.ReadingId;

    /// <summary>Gets a value indicating whether the model-only reading is the labeled one.</summary>
    public bool ModelOnlyCorrect => ModelOnly == Overlay.Row.ReadingId;
}

/// <summary>One slice of an overlay with and without the curated cue priors, with clustered intervals.</summary>
/// <param name="Name">The slice name, for example <c>all</c>, <c>unseen</c> or <c>kind:novel-vocabulary</c>.</param>
/// <param name="Rows">The rows of the slice.</param>
/// <param name="ShippedCorrect">The rows the shipped classifier decides correctly.</param>
/// <param name="ShippedAccuracy">The clustered interval of the shipped accuracy (its point estimate is the accuracy).</param>
/// <param name="ModelOnlyCorrect">The rows the model alone decides correctly.</param>
/// <param name="ModelOnlyAccuracy">The clustered interval of the model-only accuracy.</param>
/// <param name="CueEffect">The paired clustered interval of the shipped minus the model-only accuracy.</param>
/// <param name="CueFired">The rows where a cue prior is added to at least one candidate.</param>
/// <param name="Changed">The rows where the shipped and the model-only readings differ.</param>
internal sealed record OverlaySlice(
    string Name,
    int Rows,
    int ShippedCorrect,
    BootstrapInterval ShippedAccuracy,
    int ModelOnlyCorrect,
    BootstrapInterval ModelOnlyAccuracy,
    BootstrapInterval CueEffect,
    int CueFired,
    int Changed);

/// <summary>The evaluation of an overlay: the shipped evaluation, every row with and without the cue priors, and the slices.</summary>
/// <param name="Shipped">The evaluation of the shipped classifier, end to end, as for a data set, with the accuracy interval.</param>
/// <param name="Rows">The per-row outcomes in file order.</param>
/// <param name="Slices">The slices, <c>all</c> first and the others in ordinal order.</param>
/// <param name="CueUnsolvableRows">The rows marked as not solvable by the curated cues, all verified.</param>
internal sealed record OverlayEvaluation(
    SetEvaluation Shipped,
    IReadOnlyList<OverlayRowOutcome> Rows,
    IReadOnlyList<OverlaySlice> Slices,
    int CueUnsolvableRows);

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

    /// <summary>The tag code of <c>NOUN</c>, for the mirror of the noun-form cue.</summary>
    private static readonly byte NounTag = HeteronymContext.UposCode("NOUN");

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
        var file = SetFiles.GetValueOrDefault(set, set);
        return EvaluateEndToEnd(set, Prepare(set), image, row => ClusterOf(row, file), withAccuracyInterval: false);
    }

    /// <summary>
    ///     Evaluates a model on caller-supplied rows through the analyzer, with caller-supplied clustering, exactly as
    ///     <see cref="Evaluate"/> does for a data file, and adds the clustered interval of the accuracy itself.
    /// </summary>
    /// <param name="set">The set name; the prepared rows are cached under it, so one name must always mean the same rows.</param>
    /// <param name="rows">The labeled rows; each target must be a form of the inventory and each label one of its candidates.</param>
    /// <param name="image">The model file bytes (compressed or not).</param>
    /// <param name="clusterOf">Names the cluster of a row; rows with the same name resample together.</param>
    /// <returns>The evaluation, with <see cref="SetEvaluation.AccuracyInterval"/> set.</returns>
    /// <exception cref="InvalidDataException">A row is not a form of the inventory or has a label that is not a candidate, or the model does not load.</exception>
    public SetEvaluation EvaluateRows(string set, IReadOnlyList<LabeledRow> rows, byte[] image, Func<LabeledRow, string> clusterOf)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(clusterOf);
        return EvaluateEndToEnd(set, PrepareRows(set, rows), image, clusterOf, withAccuracyInterval: true);
    }

    /// <summary>
    ///     Evaluates a model on caller-supplied rows with the direct path only, as the multi-seed study does.
    /// </summary>
    /// <param name="set">The set name (see <see cref="EvaluateRows"/>).</param>
    /// <param name="rows">The labeled rows.</param>
    /// <param name="image">The model file bytes.</param>
    /// <param name="clusterOf">Names the cluster of a row.</param>
    /// <returns>The evaluation; its parity counts are zero and it has no accuracy interval.</returns>
    /// <exception cref="InvalidDataException">A row is not a form of the inventory or has a label that is not a candidate, or the model does not load.</exception>
    public SetEvaluation EvaluateRowsDirect(string set, IReadOnlyList<LabeledRow> rows, byte[] image, Func<LabeledRow, string> clusterOf)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(clusterOf);
        return EvaluateDirectRows(set, PrepareRows(set, rows), image, clusterOf);
    }

    /// <summary>
    ///     Evaluates an evaluation overlay with the shipped classifier (with its curated cue priors) and with the model
    ///     alone (without them), per row and per slice, and verifies the rows that the curated cues cannot solve.
    /// </summary>
    /// <param name="set">The overlay set name; the prepared rows are cached under it.</param>
    /// <param name="rows">The overlay rows, with unique ids.</param>
    /// <param name="image">The model file bytes (compressed or not).</param>
    /// <returns>The overlay evaluation.</returns>
    /// <exception cref="InvalidDataException">
    ///     A row id repeats, a row is not a form of the inventory or has a label that is not a candidate, the model does
    ///     not load, or a row marked as not solvable by the curated cues is one they can affect (the message names every
    ///     such row).
    /// </exception>
    /// <remarks>
    ///     <para>
    ///     <b>Shipped.</b> The analyzer's end-to-end reading with the model under test, exactly as <see cref="EvaluateRows"/>
    ///     measures it; the rows cluster by <see cref="OverlayRow.Cluster"/>.
    ///     </para>
    ///     <para>
    ///     <b>Model only.</b> The same decision without the curated cue priors: the candidate with the highest integer
    ///     score over the row's own features and the loaded weights (<see cref="AveragedPerceptronTrainer.Decide"/>, the
    ///     scoring the classifier itself uses), on the same path, so a row the analyzer leaves without a reading stays
    ///     without one and a row whose target is not one runtime token keeps the analyzer's reading (its context is not
    ///     available). It is an evaluation-only computation: the classifier, the model and the analyzer are not changed.
    ///     </para>
    ///     <para>
    ///     <b>Cue detection.</b> A row's cue fires when one of the classifier's four priors is added to at least one
    ///     candidate: the adjective cue, the noun-form cue, the past-time cue or a context cue. The noun-form test is
    ///     private to the classifier and is mirrored here from the same inventory facts; a row where no cue fires must
    ///     get the same reading from the classifier and from the model alone, and a difference stops the evaluation,
    ///     so the mirror cannot drift silently.
    ///     </para>
    ///     <para>
    ///     <b>Cue-unsolvable rows.</b> A row marked as not solvable by the curated cues must be a single runtime token
    ///     on which neither the adjective, the past-time nor any context cue test fires, on which the noun-form prior is
    ///     not added to any candidate (whether or not it would change the reading), and on which the classifier and the
    ///     model alone choose the same reading; any other row is a data error naming the cue.
    ///     </para>
    /// </remarks>
    public OverlayEvaluation EvaluateOverlay(string set, IReadOnlyList<OverlayRow> rows, byte[] image)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(image);

        var overlays = new Dictionary<string, OverlayRow>(StringComparer.Ordinal);
        var repeated = rows.FirstOrDefault(row => !overlays.TryAdd(row.Row.Id, row));
        if (repeated is not null)
        {
            throw new InvalidDataException($"{set}: the row id '{repeated.Row.Id}' occurs twice; run check-data");
        }

        string ClusterOfRow(LabeledRow row) => overlays[row.Id].Cluster;
        var prepared = PrepareRows(set, rows.Select(static row => row.Row).ToList());
        var (outcomes, parity) = RunEndToEnd(prepared, image);
        var shipped = Summarize(outcomes, parity, set, ClusterOfRow, withAccuracyInterval: true);

        var model = SparsePerceptronModel.Load(image);
        var classifier = new HeteronymClassifier(_corpus.Inventory, model);
        var weights = new LoadedModelWeights(model);
        var results = new List<OverlayRowOutcome>(rows.Count);
        var violations = new List<string>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = prepared[i];
            var emitted = outcomes[i].Reading;
            var cueFired = false;
            var modelOnly = emitted;
            if (row.IsAligned)
            {
                var scores = new int[row.Candidates.Count];
                var alone = row.Candidates[AveragedPerceptronTrainer.Decide(_corpus.Inventory, row, weights, model.Mask, scores)];
                var withCues = classifier.Classify(row.Context!, row.Target, row.FormIndex).ReadingIndex;
                cueFired = AnyCueFires(row);
                if (!cueFired && withCues != alone)
                {
                    throw new InvalidDataException(
                        $"{set}: row {row.Row.Id}: no cue prior was detected but the classifier and the model alone disagree; the cue detection of the evaluator no longer matches the classifier");
                }

                modelOnly = emitted < 0 ? -1 : alone;
                var test = rows[i].CueUnsolvable ? FiringCueTest(row) : null;
                if (test is not null)
                {
                    violations.Add($"{row.Row.Id} ({test})");
                }
                else if (rows[i].CueUnsolvable && withCues != alone)
                {
                    violations.Add($"{row.Row.Id} (a cue prior changes the reading)");
                }
            }
            else if (rows[i].CueUnsolvable)
            {
                violations.Add($"{row.Row.Id} (not one runtime token)");
            }

            results.Add(new OverlayRowOutcome(rows[i], ReadingName(emitted), ReadingName(modelOnly), cueFired));
        }

        if (violations.Count > 0)
        {
            throw new InvalidDataException(
                $"{set}: {violations.Count} rows marked cue_unsolvable=yes can be affected by a curated cue: {string.Join(", ", violations)}");
        }

        return new OverlayEvaluation(shipped, results, Slices(results), rows.Count(static row => row.CueUnsolvable));
    }

    /// <summary>
    ///     Names a reading index for an overlay outcome.
    /// </summary>
    /// <param name="reading">The reading index, or -1 for none.</param>
    /// <returns>The reading id, or <c>none</c>.</returns>
    private string ReadingName(int reading) => reading < 0 ? "none" : _corpus.Inventory.ReadingIdAt(reading);

    /// <summary>
    ///     Names the cue test that fires on an aligned row: the adjective, past-time or context cue test whatever the
    ///     row's candidates, or the noun-form cue when the classifier adds its prior to a candidate.
    /// </summary>
    /// <remarks>
    ///     The noun-form cue is reported whether or not it changes the reading, so a row marked as not solvable by the
    ///     curated cues cannot carry it unnoticed just because the model alone already chooses the same reading.
    /// </remarks>
    /// <param name="row">The aligned prepared row.</param>
    /// <returns>The name of the first test that fires, or <see langword="null"/> when none does.</returns>
    private string? FiringCueTest(PreparedRow row)
    {
        ReadOnlySpan<ContextToken> context = row.Context!;
        if (HeteronymClassifier.HasAdjectiveCue(context, row.Target))
        {
            return "adjective cue";
        }

        if (HeteronymClassifier.HasPastTimeCue(context, row.Target))
        {
            return "past-time cue";
        }

        if (row.Candidates.Any(candidate => HeteronymClassifier.HasContextCue(row.Context!, row.Target, _corpus.Inventory.ReadingIdAt(candidate))))
        {
            return "context cue";
        }

        // The noun-form prior is a cue acting on the row even when the model alone already prefers the same reading
        return NounFormCueApplies(row) ? "noun-form cue" : null;
    }

    /// <summary>
    ///     Tells whether one of the classifier's four cue priors is added to at least one candidate of an aligned row.
    /// </summary>
    /// <param name="row">The aligned prepared row.</param>
    /// <returns><see langword="true"/> when a prior is added to a candidate.</returns>
    private bool AnyCueFires(PreparedRow row)
    {
        var inventory = _corpus.Inventory;
        ReadOnlySpan<ContextToken> context = row.Context!;
        if (HeteronymClassifier.HasAdjectiveCue(context, row.Target) && row.Candidates.Any(inventory.IsAdjective))
        {
            return true;
        }

        if (HeteronymClassifier.HasPastTimeCue(context, row.Target) && row.Candidates.Any(inventory.IsPastTense))
        {
            return true;
        }

        return row.Candidates.Any(candidate => HeteronymClassifier.HasContextCue(row.Context!, row.Target, inventory.ReadingIdAt(candidate)))
            || NounFormCueApplies(row);
    }

    /// <summary>
    ///     Mirrors the classifier's noun-form cue: the word is tagged <c>NOUN</c>, exactly one candidate can be a noun of
    ///     the form, and another candidate is a noun of its lemma only in other forms.
    /// </summary>
    /// <param name="row">The aligned prepared row.</param>
    /// <returns><see langword="true"/> when the classifier adds the noun-form prior to a candidate.</returns>
    private bool NounFormCueApplies(PreparedRow row)
    {
        if (row.Context![row.Target].Upos != NounTag)
        {
            return false;
        }

        var inventory = _corpus.Inventory;
        var nouns = 0;
        var ruledOut = false;
        foreach (var candidate in row.Candidates)
        {
            if (inventory.CanBeNoun(row.FormIndex, candidate))
            {
                nouns++;
            }
            else
            {
                ruledOut |= inventory.IsNounOfItsLemma(candidate);
            }
        }

        return nouns == 1 && ruledOut;
    }

    /// <summary>
    ///     Reduces the per-row outcomes of an overlay to its slices.
    /// </summary>
    /// <param name="results">The per-row outcomes.</param>
    /// <returns>The slices, <c>all</c> first and the others in ordinal order.</returns>
    private List<OverlaySlice> Slices(IReadOnlyList<OverlayRowOutcome> results)
    {
        var names = results.SelectMany(static result => result.Overlay.Slices).Where(static name => name != "all").Distinct().Order(StringComparer.Ordinal);
        var slices = new List<OverlaySlice>();
        foreach (var name in new[] { "all" }.Concat(names))
        {
            var members = results.Where(result => result.Overlay.Slices.Contains(name, StringComparer.Ordinal)).ToList();
            var order = new List<string>();
            var counts = new Dictionary<string, int[]>(StringComparer.Ordinal);
            foreach (var member in members)
            {
                if (!counts.TryGetValue(member.Overlay.Cluster, out var count))
                {
                    count = new int[3];
                    counts[member.Overlay.Cluster] = count;
                    order.Add(member.Overlay.Cluster);
                }

                count[0]++;
                count[1] += member.ShippedCorrect ? 1 : 0;
                count[2] += member.ModelOnlyCorrect ? 1 : 0;
            }

            var shipped = order.Select(id => new ClusterStats(id, counts[id][0], counts[id][1], 0)).ToList();
            var alone = order.Select(id => new ClusterStats(id, counts[id][0], counts[id][2], 0)).ToList();
            var paired = order.Select(id => new ClusterStats(id, counts[id][0], counts[id][1], counts[id][2])).ToList();
            slices.Add(new OverlaySlice(
                name,
                members.Count,
                members.Count(static member => member.ShippedCorrect),
                Bootstrap(shipped, _settings),
                members.Count(static member => member.ModelOnlyCorrect),
                Bootstrap(alone, _settings),
                Bootstrap(paired, _settings),
                members.Count(static member => member.CueFired),
                members.Count(static member => member.Shipped != member.ModelOnly)));
        }

        return slices;
    }

    /// <summary>
    ///     Evaluates prepared rows through the analyzer and checks parity with the direct path.
    /// </summary>
    /// <param name="set">The set name.</param>
    /// <param name="rows">The prepared rows.</param>
    /// <param name="image">The model file bytes.</param>
    /// <param name="clusterOf">Names the cluster of a row.</param>
    /// <param name="withAccuracyInterval">Whether to compute the interval of the accuracy itself.</param>
    /// <returns>The evaluation.</returns>
    private SetEvaluation EvaluateEndToEnd(string set, IReadOnlyList<PreparedRow> rows, byte[] image, Func<LabeledRow, string> clusterOf, bool withAccuracyInterval)
    {
        var (outcomes, parity) = RunEndToEnd(rows, image);
        return Summarize(outcomes, parity, set, clusterOf, withAccuracyInterval);
    }

    /// <summary>
    ///     Runs prepared rows through the analyzer and checks parity with the direct path.
    /// </summary>
    /// <param name="rows">The prepared rows.</param>
    /// <param name="image">The model file bytes.</param>
    /// <returns>The per-row outcomes in order and the parity counts.</returns>
    private (List<Outcome> Outcomes, ParityCounts Parity) RunEndToEnd(IReadOnlyList<PreparedRow> rows, byte[] image)
    {
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

        return (outcomes, new ParityCounts(compared, mismatched, suppressed));
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
        var file = SetFiles.GetValueOrDefault(set, set);
        return EvaluateDirectRows(set, Prepare(set), image, row => ClusterOf(row, file));
    }

    /// <summary>
    ///     Evaluates prepared rows with the direct path only.
    /// </summary>
    /// <param name="set">The set name.</param>
    /// <param name="rows">The prepared rows.</param>
    /// <param name="image">The model file bytes.</param>
    /// <param name="clusterOf">Names the cluster of a row.</param>
    /// <returns>The evaluation; its parity counts are zero.</returns>
    private SetEvaluation EvaluateDirectRows(string set, IReadOnlyList<PreparedRow> rows, byte[] image, Func<LabeledRow, string> clusterOf)
    {
        var classifier = CreateClassifier(image);
        var outcomes = new List<Outcome>(rows.Count);
        foreach (var row in rows)
        {
            var direct = Direct(classifier, row);
            outcomes.Add(new Outcome(row, direct?.ReadingIndex ?? -1, direct?.Confidence ?? 0.0));
        }

        return Summarize(outcomes, new ParityCounts(0, 0, 0), set, clusterOf, withAccuracyInterval: false);
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
        row.IsAligned ? classifier.Classify(row.Context!, row.Target, row.FormIndex) : null;

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

        var prepared = PrepareRows(set, RowPreparer.ReadRows(Path.Combine(_dataRoot, file), file));
        return prepared;
    }

    /// <summary>
    ///     Prepares rows once under a set name, keeping the classifier contexts.
    /// </summary>
    /// <param name="set">The set name the prepared rows are cached under.</param>
    /// <param name="rows">The labeled rows.</param>
    /// <returns>The prepared rows in order.</returns>
    /// <exception cref="InvalidDataException">A row is not a form of the inventory or its label is not a candidate of the form.</exception>
    private IReadOnlyList<PreparedRow> PrepareRows(string set, IReadOnlyList<LabeledRow> rows)
    {
        if (_prepared.TryGetValue(set, out var cached))
        {
            return cached;
        }

        var prepared = rows
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
    /// <param name="outcomes">The outcomes in file order.</param>
    /// <param name="parity">The parity counts.</param>
    /// <param name="set">The set name.</param>
    /// <param name="clusterOf">Names the cluster of a row.</param>
    /// <param name="withAccuracyInterval">Whether to compute the interval of the accuracy itself.</param>
    /// <returns>The evaluation.</returns>
    private SetEvaluation Summarize(List<Outcome> outcomes, ParityCounts parity, string set, Func<LabeledRow, string> clusterOf, bool withAccuracyInterval)
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

            var cluster = clusterOf(row.Row);
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

        var clusterStats = clusterOrder.Select(id => new ClusterStats(id, clusters[id][0], clusters[id][1], clusters[id][2])).ToList();
        var interval = Bootstrap(clusterStats, _settings);

        // The interval of the accuracy itself: the same resampling with the baseline counts taken as zero.
        BootstrapInterval? accuracyInterval = withAccuracyInterval
            ? Bootstrap(clusterStats.Select(static c => c with { BaselineCorrect = 0 }).ToList(), _settings)
            : null;
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
            parity,
            accuracyInterval);
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

    /// <summary>The weights of a loaded model as a weight source, for the model-only decision.</summary>
    /// <param name="Model">The model.</param>
    private readonly record struct LoadedModelWeights(SparsePerceptronModel Model) : HeteronymClassifier.IWeightSource
    {
        /// <inheritdoc/>
        public int GetWeight(uint bucket) => Model.GetWeight(bucket);
    }

    /// <summary>One row's end-to-end result.</summary>
    /// <param name="Row">The prepared row.</param>
    /// <param name="Reading">The emitted reading index, or -1.</param>
    /// <param name="Confidence">The reported confidence; 0 when none.</param>
    private sealed record Outcome(PreparedRow Row, int Reading, double Confidence);
}
