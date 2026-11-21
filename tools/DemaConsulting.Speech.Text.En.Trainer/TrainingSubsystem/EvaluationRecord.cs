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

    /// <summary>
    ///     Renders the record files.
    /// </summary>
    /// <param name="report">The evaluation. Must not be null.</param>
    /// <returns>The file contents by file name, in ordinal name order.</returns>
    public static IReadOnlyDictionary<string, string> Render(EvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            [SummaryFile] = RenderSummary(report),
            [GateFile] = RenderGate(report),
            [BySourceFile] = RenderGroups(report, "source", static set => set.BySource),
            [ByReadingFile] = RenderGroups(report, "reading", static set => set.ByReading),
            [ConfidenceFile] = RenderGroups(report, "confidence", static set => set.ByConfidence),
            [SeedsFile] = RenderSeeds(report),
            [OverlayRowsFile] = RenderOverlayRows(report),
            [CuratedAblationFile] = RenderCuratedAblation(report),
        };
        return files;
    }

    /// <summary>
    ///     Writes the record files into a folder, creating it when needed.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <param name="directory">The folder.</param>
    public static void Write(EvaluationReport report, string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        Directory.CreateDirectory(directory);
        foreach (var (name, content) in Render(report))
        {
            File.WriteAllBytes(Path.Combine(directory, name), new UTF8Encoding(false).GetBytes(content));
        }
    }

    /// <summary>
    ///     Compares the record files of a folder with a fresh evaluation.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <param name="directory">The folder.</param>
    /// <returns>The names of the files that are missing or differ, in ordinal order; empty when the record is current.</returns>
    public static IReadOnlyList<string> Compare(EvaluationReport report, string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var stale = new List<string>();
        foreach (var (name, content) in Render(report))
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
    ///     Renders the gate file: the image, the thresholds and the verdict on the natural holdout.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <returns>The file text.</returns>
    private static string RenderGate(EvaluationReport report)
    {
        var text = new StringBuilder();
        text.Append("key\tvalue\n");
        Row(text, "image_sha256", report.ImageSha256);
        Row(text, "gate_min_pooled_accuracy_per_thousand", report.Evaluation.GateMinPooledAccuracyPerThousand);
        Row(text, "confidence_percent", report.Evaluation.ConfidencePercent);
        Row(text, "resamples", report.Evaluation.Resamples);
        Row(text, "bootstrap_seed", report.Evaluation.BootstrapSeed);
        var natural = report.Sets.FirstOrDefault(static set => set.Set == "holdout-natural");
        if (natural is null)
        {
            Row(text, "verdict", "not evaluated");
        }
        else
        {
            var verdict = HeteronymEvaluator.DecideGate(natural, report.Evaluation);
            Row(text, "accuracy_ok", verdict.AccuracyOk ? "yes" : "no");
            Row(text, "interval_ok", verdict.IntervalOk ? "yes" : "no");
            Row(text, "verdict", verdict.Passed ? "pass" : "fail");
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
    ///     Renders the multi-seed file: one row per seed and the mean, minimum and maximum.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <returns>The file text.</returns>
    private static string RenderSeeds(EvaluationReport report)
    {
        var text = new StringBuilder();
        text.Append("seed\tnatural_rows\tnatural_correct\tnatural_bp\tindependent_rows\tindependent_correct\tindependent_bp\n");
        foreach (var seed in report.Seeds)
        {
            Row(
                text,
                seed.Seed,
                seed.NaturalRows,
                seed.NaturalCorrect,
                HeteronymEvaluator.RoundBasisPoints(seed.NaturalCorrect, seed.NaturalRows),
                seed.IndependentRows,
                seed.IndependentCorrect,
                HeteronymEvaluator.RoundBasisPoints(seed.IndependentCorrect, seed.IndependentRows));
        }

        if (report.Seeds.Count > 0)
        {
            var natural = report.Seeds.Select(static s => HeteronymEvaluator.RoundBasisPoints(s.NaturalCorrect, s.NaturalRows)).ToList();
            var independent = report.Seeds.Select(static s => HeteronymEvaluator.RoundBasisPoints(s.IndependentCorrect, s.IndependentRows)).ToList();
            Row(text, "mean", string.Empty, string.Empty, Mean(natural), string.Empty, string.Empty, Mean(independent));
            Row(text, "min", string.Empty, string.Empty, natural.Min(), string.Empty, string.Empty, independent.Min());
            Row(text, "max", string.Empty, string.Empty, natural.Max(), string.Empty, string.Empty, independent.Max());
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
