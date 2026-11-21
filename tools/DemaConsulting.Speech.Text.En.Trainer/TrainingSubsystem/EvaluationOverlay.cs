using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

/// <summary>
///     One row of an evaluation overlay, ready for the evaluator: the labeled row and what the overlay says about it.
/// </summary>
/// <param name="Row">
///     The labeled row; its <see cref="LabeledRow.Source"/> is the slice key of the row (for example
///     <c>generalization/novel-vocabulary/cue-unsolvable</c> or <c>regression/listening/unseen</c>), so the
///     by-source tallies of the evaluation are the overlay's strata.
/// </param>
/// <param name="Cluster">The bootstrap cluster: <c>family:&lt;family&gt;</c> for a generalization row, <c>row:&lt;id&gt;</c> for a regression row.</param>
/// <param name="Group">The family of a generalization row or the origin of a regression row.</param>
/// <param name="Kind">The kind of a generalization row; <c>-</c> for a regression row.</param>
/// <param name="SeenInTraining">The seen-in-training mark of a regression row (<c>yes</c> or <c>no</c>); <c>-</c> for a generalization row.</param>
/// <param name="CueUnsolvable">Whether the curated cues of the classifier cannot solve a generalization row; always false for a regression row.</param>
/// <param name="Slices">The report slices the row belongs to, <c>all</c> first.</param>
internal sealed record OverlayRow(
    LabeledRow Row,
    string Cluster,
    string Group,
    string Kind,
    string SeenInTraining,
    bool CueUnsolvable,
    IReadOnlyList<string> Slices);

/// <summary>
///     Loads the two evaluation overlays of the data folder, the regression overlay and the blind generalization set,
///     as rows the evaluator can score, with their strata, slices and bootstrap clusters.
/// </summary>
/// <remarks>
///     <para>
///     The overlays are not splits: they are read only by the evaluation and never by training or model selection.
///     The rows are read with the strict loaders of <see cref="TextEnDataSet"/>; any finding (a missing file, a wrong
///     header, an offset that is not an integer or a span outside its sentence) stops the load with an
///     <see cref="InvalidDataException"/> that names the file and suggests <c>check-data</c>, which checks everything
///     else about the overlays.
///     </para>
///     <para>
///     <b>Clusters.</b> A generalization row resamples with its family (the cue or template family the set was
///     written in), as the pre-registered criterion of the word-cluster work requires; a regression row is its own
///     cluster, because its rows come from unrelated places. <b>Slices.</b> Every row is in the slice <c>all</c>; a
///     regression row also in <c>seen</c> or <c>unseen</c> and <c>origin:&lt;origin&gt;</c>; a generalization row
///     also in <c>kind:&lt;kind&gt;</c>, <c>cue_unsolvable:yes</c> or <c>cue_unsolvable:no</c> and
///     <c>family:&lt;family&gt;</c>. Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class EvaluationOverlay
{
    /// <summary>The set name of the regression overlay.</summary>
    public const string Regression = "regression";

    /// <summary>The set name of the generalization set.</summary>
    public const string Generalization = "generalization";

    /// <summary>The overlay set names in ordinal order, the order in which the evaluation record appends them.</summary>
    public static readonly IReadOnlyList<string> Sets = [Generalization, Regression];

    /// <summary>
    ///     Tells whether a set name is an overlay.
    /// </summary>
    /// <param name="set">The set name.</param>
    /// <returns><see langword="true"/> for <c>regression</c> and <c>generalization</c>.</returns>
    public static bool IsOverlay(string set) => set is Regression or Generalization;

    /// <summary>
    ///     Gets the file name of an overlay.
    /// </summary>
    /// <param name="set">The overlay set name.</param>
    /// <returns>The file name in the data folder.</returns>
    /// <exception cref="ArgumentException"><paramref name="set"/> is not an overlay.</exception>
    public static string FileOf(string set) => set switch
    {
        Regression => TextEnDataSet.RegressionFile,
        Generalization => TextEnDataSet.GeneralizationFile,
        _ => throw new ArgumentException($"'{set}' is not an evaluation overlay", nameof(set)),
    };

    /// <summary>
    ///     Loads the rows of an overlay.
    /// </summary>
    /// <param name="dataRoot">The data folder.</param>
    /// <param name="set">The overlay set name.</param>
    /// <returns>The rows in file order; empty for a file that has its header only.</returns>
    /// <exception cref="InvalidDataException">The file is missing or malformed, or a span is not inside its sentence.</exception>
    /// <exception cref="ArgumentException"><paramref name="set"/> is not an overlay.</exception>
    public static IReadOnlyList<OverlayRow> Load(string dataRoot, string set)
    {
        ArgumentNullException.ThrowIfNull(dataRoot);
        var file = FileOf(set);
        var path = Path.Combine(dataRoot, file);
        var report = new CheckReport(10);
        if (set == Regression)
        {
            var records = TextEnDataSet.LoadRegression(path, file, report);
            ThrowOnFinding(report);
            return records.Select(ToOverlayRow).ToList();
        }

        var generalization = TextEnDataSet.LoadGeneralization(path, file, report);
        ThrowOnFinding(report);
        return generalization.Select(ToOverlayRow).ToList();
    }

    /// <summary>
    ///     Stops the load on the first finding of the strict loader.
    /// </summary>
    /// <param name="report">The report of the load.</param>
    /// <exception cref="InvalidDataException">The report has a finding.</exception>
    private static void ThrowOnFinding(CheckReport report)
    {
        if (report.FindingCount > 0)
        {
            throw new InvalidDataException($"{report.Findings[0].Location}: {report.Findings[0].Message}; run check-data");
        }
    }

    /// <summary>
    ///     Converts a regression row.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns>The overlay row.</returns>
    private static OverlayRow ToOverlayRow(RegressionRecord record)
    {
        var seen = record.SeenInTraining == "yes" ? "seen" : "unseen";
        var row = Labeled(record.Location, record.Id, record.ReadingId, record.Sentence, record.Start, record.End, $"{Regression}/{record.Origin}/{seen}");
        return new OverlayRow(row, "row:" + record.Id, record.Origin, "-", record.SeenInTraining, false, ["all", seen, "origin:" + record.Origin]);
    }

    /// <summary>
    ///     Converts a generalization row.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns>The overlay row.</returns>
    private static OverlayRow ToOverlayRow(GeneralizationRecord record)
    {
        var unsolvable = record.CueUnsolvable == "yes";
        var stratum = unsolvable ? "cue-unsolvable" : "cue-solvable";
        var row = Labeled(record.Location, record.Id, record.ReadingId, record.Sentence, record.Start, record.End, $"{Generalization}/{record.Kind}/{stratum}");
        return new OverlayRow(
            row,
            "family:" + record.Family,
            record.Family,
            record.Kind,
            "-",
            unsolvable,
            ["all", "kind:" + record.Kind, unsolvable ? "cue_unsolvable:yes" : "cue_unsolvable:no", "family:" + record.Family]);
    }

    /// <summary>
    ///     Builds the labeled row of an overlay record, checking its span.
    /// </summary>
    /// <param name="location">The <c>file:line</c> of the record.</param>
    /// <param name="id">The row id.</param>
    /// <param name="readingId">The labeled reading.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="start">The start offset, or null when it was not an integer (already a finding).</param>
    /// <param name="end">The end offset, or null when it was not an integer (already a finding).</param>
    /// <param name="source">The slice key carried as the source.</param>
    /// <returns>The labeled row.</returns>
    /// <exception cref="InvalidDataException">The span is not inside the sentence.</exception>
    private static LabeledRow Labeled(string location, string id, string readingId, string sentence, int? start, int? end, string source)
    {
        // The span is compared without arithmetic so no pair of offsets can overflow; later stages slice with it unchecked.
        if (start is not { } first || end is not { } last || first < 0 || first >= last || last > sentence.Length)
        {
            throw new InvalidDataException($"{location}: {id}: the target span is not inside the sentence; run check-data");
        }

        return new LabeledRow(id, readingId, sentence, first, last, source);
    }
}
