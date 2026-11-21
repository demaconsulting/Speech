using System.Globalization;
using System.Text;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

/// <summary>
///     Everything an evaluation record reports.
/// </summary>
/// <param name="ImageSha256">The SHA-256 (hex) of the uncompressed image of the evaluated model.</param>
/// <param name="Evaluation">The evaluation settings, including the gate threshold.</param>
/// <param name="Sets">The evaluated sets, in the order they are reported.</param>
/// <param name="Seeds">The multi-seed results; empty when the study was not run.</param>
/// <param name="Overlays">
///     The evaluation overlays with and without the curated cue priors, in the order they are reported; null or empty
///     when none was evaluated. Their shipped evaluations are also in <paramref name="Sets"/>, after the data sets.
/// </param>
internal sealed record EvaluationReport(
    string ImageSha256,
    EvaluationSettings Evaluation,
    IReadOnlyList<SetEvaluation> Sets,
    IReadOnlyList<SeedResult> Seeds,
    IReadOnlyList<OverlayEvaluation>? Overlays = null);

/// <summary>One row of a gate set with its labeled reading and the readings of the previous and the current model.</summary>
/// <param name="Set">The gate set.</param>
/// <param name="Id">The row id.</param>
/// <param name="Gold">The labeled reading id.</param>
/// <param name="Previous">The reading of the previous model, or <c>-</c> for a row the previous record does not have.</param>
/// <param name="Current">The reading of the current model, or <c>none</c>.</param>
internal sealed record GateRowState(string Set, string Id, string Gold, string Previous, string Current);

/// <summary>The gate state of a committed evaluation record, which the next record rotates from.</summary>
/// <param name="ImageSha256">The image of the committed record's model.</param>
/// <param name="PreviousImageSha256">The image of the model its previous column belongs to.</param>
/// <param name="Rows">The committed gate rows by set and id.</param>
internal sealed record CommittedGate(string ImageSha256, string PreviousImageSha256, IReadOnlyDictionary<(string Set, string Id), GateRowState> Rows);

/// <summary>
///     Renders an evaluation as a small set of deterministic tab-separated files, and compares such files with a
///     folder, so a committed evaluation record can be regenerated and verified.
/// </summary>
/// <remarks>
///     <para>
///     Every number is an integer or an integer ratio in basis points (hundredths of a percent) rounded half away
///     from zero, so no floating-point formatting reaches a file and the files are identical on every platform. The
///     files have a fixed column order, LF line endings, no byte order mark and a final newline, and hold no time
///     stamp, host name or duration. Stateless and thread-safe.
///     </para>
///     <para>
///     The files are plain tables rather than Markdown so that the repository's text formatters cannot reflow them
///     and break the byte-for-byte comparison. The evaluation overlays come after the data sets in every shared file,
///     so adding them leaves every earlier line byte-identical.
///     </para>
/// </remarks>
internal static class EvaluationRecord
{
    /// <summary>The summary file.</summary>
    public const string SummaryFile = "summary.tsv";

    /// <summary>The gate decision file.</summary>
    public const string GateFile = "gate.tsv";

    /// <summary>The per-source file.</summary>
    public const string BySourceFile = "by-source.tsv";

    /// <summary>The per-reading file.</summary>
    public const string ByReadingFile = "per-reading.tsv";

    /// <summary>The per-confidence file.</summary>
    public const string ConfidenceFile = "confidence.tsv";

    /// <summary>The multi-seed file.</summary>
    public const string SeedsFile = "seeds.tsv";

    /// <summary>The per-row file of the evaluation overlays.</summary>
    public const string OverlayRowsFile = "overlay-rows.tsv";

    /// <summary>The file of the overlay slices with and without the curated cue priors.</summary>
    public const string CuratedAblationFile = "curated-ablation.tsv";

    /// <summary>The per-row file of the gate sets: the labeled, previous and current reading of every row.</summary>
    public const string GateRowsFile = "gate-rows.tsv";

    /// <summary>The flip report: the gate rows whose reading changed from the previous model to the current one.</summary>
    public const string FlipsFile = "flips.tsv";

    /// <summary>The cell of a row the previous record does not have.</summary>
    private const string NewRow = "-";

    /// <summary>The header of the gate-rows file.</summary>
    private const string GateRowsHeader = "set\tid\tgold\tprevious\tcurrent";

    /// <summary>
    ///     Renders the record files as a first write: the previous model is the current one, so there is no flip.
    /// </summary>
    /// <param name="report">The evaluation. Must not be null.</param>
    /// <returns>The file contents by file name, in ordinal name order.</returns>
    public static IReadOnlyDictionary<string, string> Render(EvaluationReport report) => Render(report, null);

    /// <summary>
    ///     Renders the record files against the committed gate state of the record folder.
    /// </summary>
    /// <param name="report">The evaluation. Must not be null.</param>
    /// <param name="committed">The committed gate state, or null for a first write.</param>
    /// <returns>The file contents by file name, in ordinal name order.</returns>
    /// <remarks>
    ///     The previous column rotates: on a first write it equals the current reading; when the committed record is of
    ///     the same model image its previous column and previous image are kept (a new row gets <c>-</c>); when the
    ///     committed record is of another image, its current column becomes the previous one and its image the previous
    ///     image.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> Render(EvaluationReport report, CommittedGate? committed)
    {
        ArgumentNullException.ThrowIfNull(report);
        var gateRows = GateRows(report, committed, out var previousImage);
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            [SummaryFile] = RenderSummary(report),
            [GateFile] = RenderGate(report, gateRows, previousImage),
            [BySourceFile] = RenderGroups(report, "source", static set => set.BySource),
            [ByReadingFile] = RenderGroups(report, "reading", static set => set.ByReading),
            [ConfidenceFile] = RenderGroups(report, "confidence", static set => set.ByConfidence),
            [SeedsFile] = RenderSeeds(report),
            [OverlayRowsFile] = RenderOverlayRows(report),
            [CuratedAblationFile] = RenderCuratedAblation(report),
            [GateRowsFile] = RenderGateRows(gateRows),
            [FlipsFile] = RenderFlips(gateRows),
        };
        return files;
    }

    /// <summary>
    ///     Reads the committed gate state of a record folder: the model image of <c>gate.tsv</c> and its previous image,
    ///     and the rows of <c>gate-rows.tsv</c>.
    /// </summary>
    /// <param name="directory">The record folder.</param>
    /// <returns>The committed state, or null when either file is missing or does not have the expected form.</returns>
    public static CommittedGate? ReadCommitted(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var gatePath = Path.Combine(directory, GateFile);
        var rowsPath = Path.Combine(directory, GateRowsFile);
        if (!File.Exists(gatePath) || !File.Exists(rowsPath))
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(gatePath).Skip(1))
        {
            var cells = line.Split('\t');
            if (cells.Length == 2)
            {
                values[cells[0]] = cells[1];
            }
        }

        var lines = File.ReadAllLines(rowsPath);
        if (!values.TryGetValue("image_sha256", out var image) || !values.TryGetValue("previous_image_sha256", out var previousImage)
            || lines.Length == 0 || lines[0] != GateRowsHeader)
        {
            return null;
        }

        var rows = new Dictionary<(string Set, string Id), GateRowState>();
        foreach (var line in lines.Skip(1).Where(static line => line.Length > 0))
        {
            var cells = line.Split('\t');
            if (cells.Length != 5)
            {
                return null;
            }

            rows[(cells[0], cells[1])] = new GateRowState(cells[0], cells[1], cells[2], cells[3], cells[4]);
        }

        return new CommittedGate(image, previousImage, rows);
    }

    /// <summary>
    ///     Writes the record files into a folder, creating it when needed.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <param name="directory">The folder.</param>
    public static void Write(EvaluationReport report, string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var files = Render(report, ReadCommitted(directory));
        Directory.CreateDirectory(directory);
        foreach (var (name, content) in files)
        {
            File.WriteAllBytes(Path.Combine(directory, name), new UTF8Encoding(false).GetBytes(content));
        }
    }

    /// <summary>
    ///     Compares the record files of a folder with a fresh evaluation, rendered against the folder's committed gate state.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <param name="directory">The folder.</param>
    /// <returns>The names of the files that are missing or differ, in ordinal order; empty when the record is current.</returns>
    public static IReadOnlyList<string> Compare(EvaluationReport report, string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var stale = new List<string>();
        foreach (var (name, content) in Render(report, ReadCommitted(directory)))
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(new UTF8Encoding(false).GetBytes(content)))
            {
                stale.Add(name);
            }
        }

        return stale;
    }

    /// <summary>
    ///     Formats basis points as a percentage with two decimals and an optional sign.
    /// </summary>
    /// <param name="basisPoints">The value in basis points.</param>
    /// <param name="signed">Whether to show a plus sign for a positive value.</param>
    /// <returns>For example <c>93.12%</c> or <c>+2.46%</c>.</returns>
    public static string FormatPercent(int basisPoints, bool signed = false)
    {
        var magnitude = Math.Abs(basisPoints);
        var sign = string.Empty;
        if (basisPoints < 0)
        {
            sign = "-";
        }
        else if (signed && basisPoints > 0)
        {
            sign = "+";
        }
        return string.Create(CultureInfo.InvariantCulture, $"{sign}{magnitude / 100}.{magnitude % 100:00}%");
    }

    /// <summary>
    ///     Renders the summary file: one row per set.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <returns>The file text.</returns>
    private static string RenderSummary(EvaluationReport report)
    {
        var text = new StringBuilder();
        text.Append("set\trows\taligned\tsingle_candidate\tcovered\tcorrect\taccuracy_bp\tupos_baseline_correct\tupos_baseline_bp\tmajority_baseline_correct\tmajority_baseline_bp\tfirst_baseline_correct\tfirst_baseline_bp\tdifference_bp\tci_lower_bp\tci_upper_bp\tci_lower_positive\tclusters\tparity_compared\tparity_mismatched\tparity_suppressed\n");
        foreach (var set in report.Sets)
        {
            Row(
                text,
                set.Set,
                set.Rows,
                set.Aligned,
                set.SingleCandidate,
                set.Covered,
                set.Correct,
                HeteronymEvaluator.RoundBasisPoints(set.Correct, set.Rows),
                set.UposBaselineCorrect,
                HeteronymEvaluator.RoundBasisPoints(set.UposBaselineCorrect, set.Rows),
                set.MajorityBaselineCorrect,
                HeteronymEvaluator.RoundBasisPoints(set.MajorityBaselineCorrect, set.Rows),
                set.FirstBaselineCorrect,
                HeteronymEvaluator.RoundBasisPoints(set.FirstBaselineCorrect, set.Rows),
                set.Interval.DifferenceBasisPoints,
                set.Interval.LowerBasisPoints,
                set.Interval.UpperBasisPoints,
                set.Interval.LowerIsPositive ? "yes" : "no",
                set.Interval.Clusters,
                set.Parity.Compared,
                set.Parity.Mismatched,
                set.Parity.Suppressed);
        }

        return text.ToString();
    }

    /// <summary>
    ///     Renders the gate file: the images, the bootstrap settings, and per gate set the counts, the accuracy with its
    ///     interval, the threshold, the flips and the decision, then the verdict.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <param name="gateRows">The rotated gate rows.</param>
    /// <param name="previousImage">The image of the previous column.</param>
    /// <returns>The file text.</returns>
    private static string RenderGate(EvaluationReport report, IReadOnlyList<GateRowState> gateRows, string previousImage)
    {
        var text = new StringBuilder();
        text.Append("key\tvalue\n");
        Row(text, "image_sha256", report.ImageSha256);
        Row(text, "previous_image_sha256", previousImage);
        Row(text, "confidence_percent", report.Evaluation.ConfidencePercent);
        Row(text, "resamples", report.Evaluation.Resamples);
        Row(text, "bootstrap_seed", report.Evaluation.BootstrapSeed);
        var evaluated = report.Evaluation.GateThresholds.All(threshold => report.Sets.Any(set => set.Set == threshold.Set && set.AccuracyInterval is not null));
        if (!evaluated)
        {
            Row(text, "verdict", "not evaluated");
            return text.ToString();
        }

        var verdict = HeteronymEvaluator.DecideGate(report.Sets, report.Evaluation);
        foreach (var set in verdict.Sets)
        {
            var changes = gateRows.Where(row => row.Set == set.Set).Select(static row => HeteronymEvaluator.ClassifyFlip(row.Gold, row.Previous, row.Current)).ToList();
            Row(text, $"{set.Set}_rows", set.Rows);
            Row(text, $"{set.Set}_clusters", set.Accuracy.Clusters);
            Row(text, $"{set.Set}_correct", set.Correct);
            Row(text, $"{set.Set}_accuracy_bp", HeteronymEvaluator.RoundBasisPoints(set.Correct, set.Rows));
            Row(text, $"{set.Set}_ci_lower_bp", set.Accuracy.LowerBasisPoints);
            Row(text, $"{set.Set}_ci_upper_bp", set.Accuracy.UpperBasisPoints);
            Row(text, $"{set.Set}_threshold_bp", set.ThresholdBasisPoints);
            Row(text, $"{set.Set}_fixed", changes.Count(static change => change == "fixed"));
            Row(text, $"{set.Set}_broken", changes.Count(static change => change == "broken"));
            Row(text, $"{set.Set}_pass", YesNo(set.Passed));
        }

        Row(text, "verdict", verdict.Passed ? "pass" : "fail");
        return text.ToString();
    }

    /// <summary>
    ///     Builds the gate rows of a report, with the previous column rotated from the committed state.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <param name="committed">The committed gate state, or null for a first write.</param>
    /// <param name="previousImage">Receives the image of the previous column.</param>
    /// <returns>The rows of every evaluated gate set, ordered by set and then id (ordinal).</returns>
    internal static IReadOnlyList<GateRowState> GateRows(EvaluationReport report, CommittedGate? committed, out string previousImage)
    {
        var sameModel = committed is not null && string.Equals(committed.ImageSha256, report.ImageSha256, StringComparison.Ordinal);
        previousImage = report.ImageSha256;
        if (committed is not null)
        {
            previousImage = sameModel ? committed.PreviousImageSha256 : committed.ImageSha256;
        }

        var rows = new List<GateRowState>();
        foreach (var set in report.Sets.Where(static set => HeteronymEvaluator.GateSets.Contains(set.Set, StringComparer.Ordinal)))
        {
            foreach (var row in set.RowReadings ?? [])
            {
                string previous;
                if (committed is null)
                {
                    previous = row.Emitted;
                }
                else if (committed.Rows.TryGetValue((set.Set, row.Id), out var old))
                {
                    previous = sameModel ? old.Previous : old.Current;
                }
                else
                {
                    previous = NewRow;
                }

                rows.Add(new GateRowState(set.Set, row.Id, row.Gold, previous, row.Emitted));
            }
        }

        return rows.OrderBy(static row => row.Set, StringComparer.Ordinal).ThenBy(static row => row.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    ///     Renders the gate-rows file: every row of the gate sets with its labeled, previous and current reading.
    /// </summary>
    /// <param name="rows">The rotated gate rows.</param>
    /// <returns>The file text.</returns>
    private static string RenderGateRows(IReadOnlyList<GateRowState> rows)
    {
        var text = new StringBuilder();
        text.Append(GateRowsHeader).Append('\n');
        foreach (var row in rows)
        {
            Row(text, row.Set, row.Id, row.Gold, row.Previous, row.Current);
        }

        return text.ToString();
    }

    /// <summary>
    ///     Renders the flip report: the gate rows whose current reading differs from the previous one, with the change.
    /// </summary>
    /// <param name="rows">The rotated gate rows.</param>
    /// <returns>The file text; the header only when nothing flipped.</returns>
    private static string RenderFlips(IReadOnlyList<GateRowState> rows)
    {
        var text = new StringBuilder();
        text.Append("set\tid\tgold\tprevious\tcurrent\tchange\n");
        foreach (var row in rows)
        {
            var change = HeteronymEvaluator.ClassifyFlip(row.Gold, row.Previous, row.Current);
            if (change is not null)
            {
                Row(text, row.Set, row.Id, row.Gold, row.Previous, row.Current, change);
            }
        }

        return text.ToString();
    }

    /// <summary>
    ///     Renders one group table (source, reading or confidence) for every set.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <param name="column">The name of the group column.</param>
    /// <param name="select">Selects the groups of a set.</param>
    /// <returns>The file text.</returns>
    internal static string RenderGroups(EvaluationReport report, string column, Func<SetEvaluation, IReadOnlyList<GroupTally>> select)
    {
        var text = new StringBuilder();
        text.Append($"set\t{column}\trows\tcorrect\taccuracy_bp\tupos_baseline_correct\n");
        foreach (var set in report.Sets)
        {
            foreach (var group in select(set))
            {
                Row(text, set.Set, group.Name, group.Rows, group.Correct, HeteronymEvaluator.RoundBasisPoints(group.Correct, group.Rows), group.BaselineCorrect);
            }
        }

        return text.ToString();
    }

    /// <summary>
    ///     Renders the multi-seed file: one row per seed with the rows, correct count and accuracy of every gate set, then
    ///     the mean, minimum and maximum accuracy and the threshold the counts give by the gate's rule.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <returns>The file text.</returns>
    private static string RenderSeeds(EvaluationReport report)
    {
        var sets = report.Seeds.SelectMany(static seed => seed.Sets.Select(static set => set.Set)).Distinct().Order(StringComparer.Ordinal).ToList();
        var text = new StringBuilder();
        text.Append("seed");
        foreach (var set in sets)
        {
            text.Append($"\t{set}_rows\t{set}_correct\t{set}_bp");
        }

        text.Append('\n');
        SeedSetCount? CountOf(SeedResult seed, string set) => seed.Sets.FirstOrDefault(count => count.Set == set);
        foreach (var seed in report.Seeds)
        {
            var cells = new List<object> { seed.Seed };
            foreach (var set in sets)
            {
                var count = CountOf(seed, set);
                cells.AddRange(count is null
                    ? new object[] { string.Empty, string.Empty, string.Empty }
                    : new object[] { count.Rows, count.Correct, HeteronymEvaluator.RoundBasisPoints(count.Correct, count.Rows) });
            }

            Row(text, [.. cells]);
        }

        if (report.Seeds.Count > 0)
        {
            var mean = new List<object> { "mean" };
            var min = new List<object> { "min" };
            var max = new List<object> { "max" };
            var rule = new List<object> { "threshold_by_rule" };
            foreach (var set in sets)
            {
                var counts = report.Seeds.Select(seed => CountOf(seed, set)).OfType<SeedSetCount>().ToList();
                var bp = counts.Select(static count => HeteronymEvaluator.RoundBasisPoints(count.Correct, count.Rows)).ToList();
                var sameRows = counts.Count >= 2 && counts.All(count => count.Rows == counts[0].Rows) && counts[0].Rows > 0;
                var threshold = sameRows ? (object)HeteronymEvaluator.DeriveGateThreshold(counts.Select(static count => count.Correct).ToList(), counts[0].Rows) : string.Empty;
                mean.AddRange(new object[] { string.Empty, string.Empty, Mean(bp) });
                min.AddRange(new object[] { string.Empty, string.Empty, bp.Min() });
                max.AddRange(new object[] { string.Empty, string.Empty, bp.Max() });
                rule.AddRange(new object[] { string.Empty, string.Empty, threshold });
            }

            Row(text, [.. mean]);
            Row(text, [.. min]);
            Row(text, [.. max]);
            Row(text, [.. rule]);
        }

        return text.ToString();
    }

    /// <summary>
    ///     Renders the per-row file of the overlays: the labeled, shipped and model-only reading of every row.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <returns>The file text; the header only when no overlay was evaluated.</returns>
    private static string RenderOverlayRows(EvaluationReport report)
    {
        var text = new StringBuilder();
        text.Append("set\tid\tgroup\tkind\tseen_in_training\tcue_unsolvable\tgold\tshipped\tmodel_only\tcue_fired\n");
        foreach (var overlay in report.Overlays ?? [])
        {
            foreach (var row in overlay.Rows)
            {
                var source = row.Overlay;
                var unsolvable = source.Kind == "-" ? "-" : YesNo(source.CueUnsolvable);
                Row(text, overlay.Shipped.Set, source.Row.Id, source.Group, source.Kind, source.SeenInTraining, unsolvable, source.Row.ReadingId, row.Shipped, row.ModelOnly, YesNo(row.CueFired));
            }
        }

        return text.ToString();
    }

    /// <summary>
    ///     Renders the slices of the overlays with the curated cue priors (the shipped model) and without them (the model
    ///     alone), with the clustered intervals of both accuracies and of their paired difference.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <returns>The file text; the header only when no overlay was evaluated.</returns>
    private static string RenderCuratedAblation(EvaluationReport report)
    {
        var text = new StringBuilder();
        text.Append("set\tslice\trows\tclusters\tshipped_correct\tshipped_bp\tshipped_ci_lower_bp\tshipped_ci_upper_bp\tmodel_only_correct\tmodel_only_bp\tmodel_only_ci_lower_bp\tmodel_only_ci_upper_bp\tcue_effect_bp\tcue_effect_ci_lower_bp\tcue_effect_ci_upper_bp\tcue_fired\tchanged\n");
        foreach (var overlay in report.Overlays ?? [])
        {
            foreach (var slice in overlay.Slices)
            {
                Row(
                    text,
                    overlay.Shipped.Set,
                    slice.Name,
                    slice.Rows,
                    slice.ShippedAccuracy.Clusters,
                    slice.ShippedCorrect,
                    slice.ShippedAccuracy.DifferenceBasisPoints,
                    slice.ShippedAccuracy.LowerBasisPoints,
                    slice.ShippedAccuracy.UpperBasisPoints,
                    slice.ModelOnlyCorrect,
                    slice.ModelOnlyAccuracy.DifferenceBasisPoints,
                    slice.ModelOnlyAccuracy.LowerBasisPoints,
                    slice.ModelOnlyAccuracy.UpperBasisPoints,
                    slice.CueEffect.DifferenceBasisPoints,
                    slice.CueEffect.LowerBasisPoints,
                    slice.CueEffect.UpperBasisPoints,
                    slice.CueFired,
                    slice.Changed);
            }
        }

        return text.ToString();
    }

    /// <summary>
    ///     Renders a flag as <c>yes</c> or <c>no</c>.
    /// </summary>
    /// <param name="value">The flag.</param>
    /// <returns>The text.</returns>
    private static string YesNo(bool value) => value ? "yes" : "no";

    /// <summary>
    ///     Computes the mean of basis-point values, rounded half up with integers.
    /// </summary>
    /// <param name="values">The values; not empty.</param>
    /// <returns>The mean.</returns>
    internal static int Mean(IReadOnlyList<int> values)
    {
        long sum = 0;
        foreach (var value in values)
        {
            sum += value;
        }

        return (int)(((2 * sum) + values.Count) / (2 * values.Count));
    }

    /// <summary>
    ///     Appends one tab-separated line with invariant-culture values.
    /// </summary>
    /// <param name="text">The text being built.</param>
    /// <param name="cells">The cells.</param>
    internal static void Row(StringBuilder text, params object[] cells)
    {
        text.Append(string.Join('\t', cells.Select(static cell => Convert.ToString(cell, CultureInfo.InvariantCulture))));
        text.Append('\n');
    }
}
