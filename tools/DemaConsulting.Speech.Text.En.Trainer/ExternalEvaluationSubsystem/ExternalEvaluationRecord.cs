using System.Text;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem;

/// <summary>
///     One external set's evaluation of the model under test and the accounting of its conversion.
/// </summary>
/// <param name="Evaluation">The end-to-end evaluation, with the accuracy interval.</param>
/// <param name="Counts">The conversion accounting.</param>
internal sealed record ExternalSetResult(SetEvaluation Evaluation, ConversionCounts Counts);

/// <summary>
///     The evaluations of one retrained model of the seed study.
/// </summary>
/// <param name="Seed">The training seed.</param>
/// <param name="Sets">The direct-path evaluation of each set, in the order of the report's sets.</param>
internal sealed record ExternalSeedResult(ulong Seed, IReadOnlyList<SetEvaluation> Sets);

/// <summary>
///     Everything the external evaluation record reports.
/// </summary>
/// <param name="ImageSha256">The SHA-256 (hex) of the uncompressed image of the evaluated model.</param>
/// <param name="Evaluation">The evaluation settings.</param>
/// <param name="Pins">The pins of the external sources.</param>
/// <param name="Sets">The evaluated sets in the order they are reported.</param>
/// <param name="Seeds">The seed study; empty when it was not run.</param>
internal sealed record ExternalEvaluationReport(
    string ImageSha256,
    EvaluationSettings Evaluation,
    ExternalSourcePins Pins,
    IReadOnlyList<ExternalSetResult> Sets,
    IReadOnlyList<ExternalSeedResult> Seeds);

/// <summary>
///     Renders the external evaluation as a small set of deterministic tab-separated files, and compares such files with a
///     folder, so a committed record can be regenerated and verified.
/// </summary>
/// <remarks>
///     <para>
///     The same conventions as <see cref="EvaluationRecord"/>: integers and basis points only, a fixed column order, LF
///     line endings, no byte order mark, a final newline, no time stamp or host name. The files hold only counts, our own
///     reading ids, set and stratum names and the pins; no sentence, target word or external label is ever written.
///     Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class ExternalEvaluationRecord
{
    /// <summary>The sources and settings file.</summary>
    public const string SourcesFile = "sources.tsv";

    /// <summary>The conversion accounting file.</summary>
    public const string ConversionFile = "conversion.tsv";

    /// <summary>The summary file.</summary>
    public const string SummaryFile = "summary.tsv";

    /// <summary>The per-stratum file.</summary>
    public const string BySourceFile = "by-source.tsv";

    /// <summary>The per-reading file.</summary>
    public const string ByReadingFile = "per-reading.tsv";

    /// <summary>The per-confidence file.</summary>
    public const string ConfidenceFile = "confidence.tsv";

    /// <summary>The seed study file.</summary>
    public const string SeedsFile = "seeds.tsv";

    /// <summary>
    ///     Renders the record files.
    /// </summary>
    /// <param name="report">The evaluation. Must not be null.</param>
    /// <returns>The file contents by file name, in ordinal name order.</returns>
    public static IReadOnlyDictionary<string, string> Render(ExternalEvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var shared = new EvaluationReport(report.ImageSha256, report.Evaluation, report.Sets.Select(static set => set.Evaluation).ToList(), []);
        return new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            [SourcesFile] = RenderSources(report),
            [ConversionFile] = RenderConversion(report),
            [SummaryFile] = RenderSummary(report),
            [BySourceFile] = EvaluationRecord.RenderGroups(shared, "source", static set => set.BySource),
            [ByReadingFile] = EvaluationRecord.RenderGroups(shared, "reading", static set => set.ByReading),
            [ConfidenceFile] = EvaluationRecord.RenderGroups(shared, "confidence", static set => set.ByConfidence),
            [SeedsFile] = RenderSeeds(report),
        };
    }

    /// <summary>
    ///     Writes the record files into a folder, creating it when needed.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <param name="directory">The folder.</param>
    public static void Write(ExternalEvaluationReport report, string directory)
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
    public static IReadOnlyList<string> Compare(ExternalEvaluationReport report, string directory)
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
    ///     Renders the sources file: the model image, the settings and the pins.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <returns>The file text.</returns>
    private static string RenderSources(ExternalEvaluationReport report)
    {
        var text = new StringBuilder();
        text.Append("key\tvalue\n");
        EvaluationRecord.Row(text, "image_sha256", report.ImageSha256);
        EvaluationRecord.Row(text, "resamples", report.Evaluation.Resamples);
        EvaluationRecord.Row(text, "confidence_percent", report.Evaluation.ConfidencePercent);
        EvaluationRecord.Row(text, "bootstrap_seed", report.Evaluation.BootstrapSeed);
        EvaluationRecord.Row(text, "seeds", string.Join(',', report.Evaluation.Seeds));
        foreach (var source in report.Pins.Sources.OrderBy(static s => s.Id, StringComparer.Ordinal))
        {
            EvaluationRecord.Row(text, source.Id + ".repository", source.Repository);
            EvaluationRecord.Row(text, source.Id + ".commit", source.Commit);
            EvaluationRecord.Row(text, source.Id + ".files", source.Files.Count);
            EvaluationRecord.Row(text, source.Id + ".bytes", source.TotalBytes);
            EvaluationRecord.Row(text, source.Id + ".files_sha256", source.FilesSha256);
            EvaluationRecord.Row(text, source.Id + ".license", source.License);
        }

        return text.ToString();
    }

    /// <summary>
    ///     Renders the conversion file: one row per set.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <returns>The file text.</returns>
    private static string RenderConversion(ExternalEvaluationReport report)
    {
        var text = new StringBuilder();
        text.Append("set\trows_read\trows_used");
        foreach (var reason in SkipReasons.All)
        {
            text.Append("\tskipped_").Append(reason.Replace('-', '_'));
        }

        text.Append("\tnon_ascii_rows\toffsets_converted\toffsets_repaired\texact_overlap_with_training\taligned\tsingle_candidate\tcovered\n");
        foreach (var set in report.Sets)
        {
            var cells = new List<object> { set.Evaluation.Set, set.Counts.Read, set.Counts.Used };
            cells.AddRange(SkipReasons.All.Select(reason => (object)set.Counts.Skipped.GetValueOrDefault(reason)));
            cells.AddRange([
                set.Counts.NonAsciiRows,
                set.Counts.OffsetsConverted,
                set.Counts.OffsetsRepaired,
                set.Counts.ExactOverlap,
                set.Evaluation.Aligned,
                set.Evaluation.SingleCandidate,
                set.Evaluation.Covered]);
            EvaluationRecord.Row(text, [.. cells]);
        }

        return text.ToString();
    }

    /// <summary>
    ///     Renders the summary file: one row per set, with the accuracy interval, the baselines and the improvement interval.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <returns>The file text.</returns>
    private static string RenderSummary(ExternalEvaluationReport report)
    {
        var text = new StringBuilder();
        text.Append("set\trows\tcorrect\taccuracy_bp\taccuracy_ci_lower_bp\taccuracy_ci_upper_bp\tupos_baseline_correct\tupos_baseline_bp\tmajority_baseline_correct\tmajority_baseline_bp\tfirst_baseline_correct\tfirst_baseline_bp\tdifference_bp\tci_lower_bp\tci_upper_bp\tci_lower_positive\tclusters\tparity_compared\tparity_mismatched\tparity_suppressed\n");
        foreach (var set in report.Sets.Select(static result => result.Evaluation))
        {
            var accuracy = set.AccuracyInterval ?? new BootstrapInterval(0, 0, 0, 0, false);
            EvaluationRecord.Row(
                text,
                set.Set,
                set.Rows,
                set.Correct,
                HeteronymEvaluator.RoundBasisPoints(set.Correct, set.Rows),
                accuracy.LowerBasisPoints,
                accuracy.UpperBasisPoints,
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
    ///     Renders the seed file: one row per set and seed, then the mean, minimum and maximum of each set.
    /// </summary>
    /// <param name="report">The evaluation.</param>
    /// <returns>The file text.</returns>
    private static string RenderSeeds(ExternalEvaluationReport report)
    {
        var text = new StringBuilder();
        text.Append("set\tseed\trows\tcorrect\taccuracy_bp\tdifference_bp\tci_lower_bp\tci_upper_bp\n");
        foreach (var name in report.Sets.Select(static result => result.Evaluation.Set))
        {
            var perSeed = report.Seeds
                .Select(seed => (seed.Seed, Set: seed.Sets.First(set => set.Set == name)))
                .ToList();
            foreach (var (seed, set) in perSeed)
            {
                EvaluationRecord.Row(
                    text,
                    name,
                    seed,
                    set.Rows,
                    set.Correct,
                    HeteronymEvaluator.RoundBasisPoints(set.Correct, set.Rows),
                    set.Interval.DifferenceBasisPoints,
                    set.Interval.LowerBasisPoints,
                    set.Interval.UpperBasisPoints);
            }

            if (perSeed.Count > 0)
            {
                var accuracy = perSeed.Select(static p => HeteronymEvaluator.RoundBasisPoints(p.Set.Correct, p.Set.Rows)).ToList();
                var difference = perSeed.Select(static p => p.Set.Interval.DifferenceBasisPoints).ToList();
                var lower = perSeed.Select(static p => p.Set.Interval.LowerBasisPoints).ToList();
                var upper = perSeed.Select(static p => p.Set.Interval.UpperBasisPoints).ToList();
                EvaluationRecord.Row(text, name, "mean", string.Empty, string.Empty, EvaluationRecord.Mean(accuracy), EvaluationRecord.Mean(difference), EvaluationRecord.Mean(lower), EvaluationRecord.Mean(upper));
                EvaluationRecord.Row(text, name, "min", string.Empty, string.Empty, accuracy.Min(), difference.Min(), lower.Min(), upper.Min());
                EvaluationRecord.Row(text, name, "max", string.Empty, string.Empty, accuracy.Max(), difference.Max(), lower.Max(), upper.Max());
            }
        }

        return text.ToString();
    }
}
