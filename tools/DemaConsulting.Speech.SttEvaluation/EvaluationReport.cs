using System.Globalization;
using System.Text;

namespace DemaConsulting.Speech.SttEvaluation;

/// <summary>
///     The outcome of one accuracy gate of <c>--check</c>.
/// </summary>
/// <param name="Scenario">The gated scenario.</param>
/// <param name="WordErrorRate">The measured corpus word error rate.</param>
/// <param name="Maximum">The highest accepted word error rate.</param>
internal sealed record GateResult(string Scenario, double WordErrorRate, double Maximum)
{
    /// <summary>Gets a value indicating whether the measured rate is within the gate.</summary>
    public bool Passed => WordErrorRate <= Maximum;
}

/// <summary>
///     Formats evaluation results for the console and writes the report files.
/// </summary>
/// <remarks>
///     Reports are written only below the output folder the caller resolved (always inside the
///     git-ignored <c>artifacts/</c> folder, see <see cref="ToolPaths.ResolveOutputDirectory"/>),
///     because engine transcripts - in particular those of third-party models - must never be
///     committed. Files are tab-separated with invariant-culture numbers so they diff and load
///     cleanly. Stateless; not safe for concurrent writes to the same folder.
/// </remarks>
internal static class EvaluationReport
{
    /// <summary>The header of the per-scenario summary table.</summary>
    private static readonly string SummaryHeader = string.Join(
        '\t',
        "model",
        "scenario",
        "files",
        "reference_words",
        "wer",
        "substitutions",
        "deletions",
        "insertions",
        "invented_words",
        "punctuated_wer",
        "segments",
        "expected_segments",
        "speech_seconds",
        "processing_seconds",
        "rtf",
        "first_result_seconds",
        "final_lag_seconds");

    /// <summary>The header of the per-recording table.</summary>
    private static readonly string ItemHeader = string.Join(
        '\t',
        "scenario",
        "id",
        "reference_words",
        "wer",
        "substitutions",
        "deletions",
        "insertions",
        "invented_words",
        "punctuated_wer",
        "segments",
        "speech_seconds",
        "processing_seconds",
        "rtf",
        "first_result_seconds",
        "final_lag_seconds");

    /// <summary>
    ///     Evaluates the accuracy gates for the gated model's results.
    /// </summary>
    /// <param name="evaluation">The gated model's results. Must not be <see langword="null"/>.</param>
    /// <returns>One result per gated scenario present in <paramref name="evaluation"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="evaluation"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<GateResult> CheckGates(ModelEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        var gates = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [EvaluationScenarios.LibriSpeech] = SttEvaluationGates.LibriSpeechMaximumWordErrorRate,
            [EvaluationScenarios.Poem] = SttEvaluationGates.CrossingTheBarMaximumWordErrorRate,
        };
        return evaluation.Scenarios
            .Where(s => gates.ContainsKey(s.Scenario))
            .Select(s => new GateResult(s.Scenario, s.Alignment.WordErrorRate, gates[s.Scenario]))
            .ToList();
    }

    /// <summary>
    ///     Writes one model's summary, per-recording table and transcripts below the output folder.
    /// </summary>
    /// <param name="outputDirectory">The resolved output folder (inside <c>artifacts/</c>).</param>
    /// <param name="evaluation">The model's results. Must not be <see langword="null"/>.</param>
    /// <returns>The folder the files were written to (<c>&lt;output&gt;/&lt;model-id&gt;</c>).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="evaluation"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">Thrown when a file cannot be written.</exception>
    /// <remarks>Creates the folder and overwrites <c>summary.tsv</c>, <c>items.tsv</c> and <c>transcripts.txt</c>.</remarks>
    public static string WriteModel(string outputDirectory, ModelEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        var directory = Path.Join(outputDirectory, evaluation.ModelId);
        Directory.CreateDirectory(directory);

        // The corpus summary, one row per scenario
        var summary = new StringBuilder().AppendLine(SummaryHeader);
        foreach (var scenario in evaluation.Scenarios)
        {
            summary.AppendLine(SummaryRow(evaluation.ModelId, scenario));
        }

        File.WriteAllText(Path.Join(directory, "summary.tsv"), summary.ToString());

        // The per-recording table and the transcripts
        var items = new StringBuilder()
            .AppendLine(ItemHeader);
        var transcripts = new StringBuilder();
        foreach (var scenario in evaluation.Scenarios)
        {
            foreach (var item in scenario.Items)
            {
                items.AppendLine(ItemRow(scenario.Scenario, item));
                AppendTranscript(transcripts, scenario.Scenario, item);
            }
        }

        File.WriteAllText(Path.Join(directory, "items.tsv"), items.ToString());
        File.WriteAllText(Path.Join(directory, "transcripts.txt"), transcripts.ToString());
        return directory;
    }

    /// <summary>
    ///     Writes the side-by-side comparison of several models below the output folder.
    /// </summary>
    /// <param name="outputDirectory">The resolved output folder (inside <c>artifacts/</c>).</param>
    /// <param name="evaluations">The models' results; the first is the baseline of the cross word error rate. Must hold at least two.</param>
    /// <returns>The path of the written <c>compare.tsv</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when fewer than two evaluations are given.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    /// <remarks>
    ///     The cross word error rate aligns each model's transcripts against the first model's
    ///     transcripts of the same recordings: it measures how differently two models transcribe
    ///     the same audio, independently of the reference.
    /// </remarks>
    public static string WriteComparison(string outputDirectory, IReadOnlyList<ModelEvaluation> evaluations)
    {
        ArgumentNullException.ThrowIfNull(evaluations);
        if (evaluations.Count < 2)
        {
            throw new ArgumentException("A comparison needs at least two models.", nameof(evaluations));
        }

        Directory.CreateDirectory(outputDirectory);
        var baseline = evaluations[0];
        var text = new StringBuilder().AppendLine(string.Join('\t', SummaryHeader, "cross_wer_vs_" + baseline.ModelId));
        foreach (var evaluation in evaluations)
        {
            foreach (var scenario in evaluation.Scenarios)
            {
                var baselineScenario = baseline.Scenarios.FirstOrDefault(s => s.Scenario == scenario.Scenario);
                var cross = baselineScenario is null ? null : (double?)CrossWordErrorRate(baselineScenario, scenario);
                text.AppendLine(string.Join('\t', SummaryRow(evaluation.ModelId, scenario), Format(cross, "F4")));
            }
        }

        var path = Path.Join(outputDirectory, "compare.tsv");
        File.WriteAllText(path, text.ToString());
        return path;
    }

    /// <summary>
    ///     Writes the console summary table of several models.
    /// </summary>
    /// <param name="writer">The writer (normally standard output). Must not be <see langword="null"/>.</param>
    /// <param name="evaluations">The models' results. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public static void WriteConsoleSummary(TextWriter writer, IReadOnlyList<ModelEvaluation> evaluations)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(evaluations);

        writer.WriteLine();
        writer.WriteLine(
            $"{"model",-58} {"scenario",-10} {"WER",7} {"punct",7} {"segments",8} {"exp",4} {"invent",6} " +
            $"{"audio s",8} {"wall s",7} {"RTF",6} {"first s",7} {"lag s",6}");
        foreach (var evaluation in evaluations)
        {
            foreach (var s in evaluation.Scenarios)
            {
                writer.WriteLine(
                    $"{evaluation.ModelId,-58} {s.Scenario,-10} {Percent(s.Alignment.WordErrorRate),7} " +
                    $"{Percent(s.VerbatimAlignment?.WordErrorRate),7} {s.Segments,8} " +
                    $"{Format(s.ExpectedSegments, "D"),4} {s.Alignment.InventedWords,6} {s.SpeechSeconds,8:F1} " +
                    $"{s.ProcessingSeconds,7:F1} {s.RealTimeFactor,6:F3} {Format(s.MeanFirstResultSeconds, "F2"),7} " +
                    $"{Format(s.MeanFinalLagSeconds, "F2"),6}");
            }
        }
    }

    /// <summary>Formats one summary row.</summary>
    /// <param name="modelId">The model id.</param>
    /// <param name="s">The scenario result.</param>
    /// <returns>The tab-separated row.</returns>
    private static string SummaryRow(string modelId, ScenarioResult s)
    {
        var a = s.Alignment;
        return string.Join(
            '\t',
            modelId,
            s.Scenario,
            Format(s.Items.Count, "D"),
            Format(a.ReferenceWords, "D"),
            Format(a.WordErrorRate, "F4"),
            Format(a.Substitutions, "D"),
            Format(a.Deletions, "D"),
            Format(a.Insertions, "D"),
            Format(a.InventedWords, "D"),
            Format(s.VerbatimAlignment?.WordErrorRate, "F4"),
            Format(s.Segments, "D"),
            Format(s.ExpectedSegments, "D"),
            Format(s.SpeechSeconds, "F2"),
            Format(s.ProcessingSeconds, "F2"),
            Format(s.RealTimeFactor, "F4"),
            Format(s.MeanFirstResultSeconds, "F2"),
            Format(s.MeanFinalLagSeconds, "F2"));
    }

    /// <summary>Formats one per-recording row.</summary>
    /// <param name="scenario">The scenario name.</param>
    /// <param name="item">The recording's result.</param>
    /// <returns>The tab-separated row.</returns>
    private static string ItemRow(string scenario, ItemResult item)
    {
        var a = item.Alignment;
        var t = item.Transcript;
        return string.Join(
            '\t',
            scenario,
            item.Input.Id,
            Format(a.ReferenceWords, "D"),
            Format(a.WordErrorRate, "F4"),
            Format(a.Substitutions, "D"),
            Format(a.Deletions, "D"),
            Format(a.Insertions, "D"),
            Format(a.InventedWords, "D"),
            Format(item.VerbatimAlignment?.WordErrorRate, "F4"),
            Format(t.SegmentCount, "D"),
            Format(t.SpeechSeconds, "F2"),
            Format(t.ProcessingTime.TotalSeconds, "F2"),
            Format(t.RealTimeFactor, "F4"),
            Format(t.FirstResultSeconds, "F2"),
            Format(t.FinalLagSeconds, "F2"));
    }

    /// <summary>Appends one recording's reference, transcript and segments to the transcript file.</summary>
    /// <param name="text">The transcript file content.</param>
    /// <param name="scenario">The scenario name.</param>
    /// <param name="item">The recording's result.</param>
    private static void AppendTranscript(StringBuilder text, string scenario, ItemResult item)
    {
        text.AppendLine(CultureInfo.InvariantCulture, $"[{scenario}] {item.Input.Id}");
        text.AppendLine(CultureInfo.InvariantCulture, $"REF: {item.Input.Reference}");
        text.AppendLine(CultureInfo.InvariantCulture, $"HYP: {item.Transcript.Text}");
        foreach (var segment in item.Transcript.Finals)
        {
            var seconds = (double)segment.SamplePosition / item.Transcript.SampleRate;
            text.AppendLine(CultureInfo.InvariantCulture, $"  final @ {seconds:F2} s: {segment.Text.Trim()}");
        }

        text.AppendLine();
    }

    /// <summary>Computes the corpus word error rate of one scenario's transcripts against another's.</summary>
    /// <param name="baseline">The baseline model's scenario (used as the reference).</param>
    /// <param name="other">The other model's scenario.</param>
    /// <returns>The cross word error rate as a fraction.</returns>
    private static double CrossWordErrorRate(ScenarioResult baseline, ScenarioResult other) =>
        baseline.Items
            .Zip(other.Items, (b, o) => WordErrorRateCalculator.Align(b.Transcript.Text, o.Transcript.Text))
            .Aggregate(WordAlignment.Empty, (sum, a) => sum.Add(a))
            .WordErrorRate;

    /// <summary>
    ///     Formats an optional fraction as a percentage with two decimals and no culture-specific
    ///     spacing (for example <c>7.84%</c>); an absent value becomes empty.
    /// </summary>
    /// <param name="fraction">The fraction (0.0784 for 7.84%), or <see langword="null"/>.</param>
    /// <returns>The formatted percentage, or an empty string.</returns>
    public static string Percent(double? fraction) =>
        fraction is { } value ? (value * 100.0).ToString("F2", CultureInfo.InvariantCulture) + "%" : string.Empty;

    /// <summary>Formats an optional number with the invariant culture; absent values become empty.</summary>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <param name="format">The numeric format string.</param>
    /// <returns>The formatted value, or an empty string.</returns>
    private static string Format(double? value, string format) =>
        value?.ToString(format, CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>Formats an optional integer with the invariant culture; absent values become empty.</summary>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <param name="format">The numeric format string.</param>
    /// <returns>The formatted value, or an empty string.</returns>
    private static string Format(int? value, string format) =>
        value?.ToString(format, CultureInfo.InvariantCulture) ?? string.Empty;
}
