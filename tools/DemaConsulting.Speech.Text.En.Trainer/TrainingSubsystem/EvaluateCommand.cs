using System.Security.Cryptography;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

/// <summary>
///     The <c>evaluate</c> command: measures a trained model on the development and holdout sets, decides the
///     quality gate, and writes or verifies the evaluation record.
/// </summary>
/// <remarks>
///     <para>
///     The model under test is read from the model directory (the committed location by default) or from
///     <c>--model</c>. Sets are scored end to end through the analyzer with baselines, strata, a paired clustered
///     bootstrap interval and a parity check; <c>--gate</c> decides the statistical quality gate on its scored sets
///     (both evaluation overlays and both holdouts, each against its recorded threshold, with the clustered interval of
///     its accuracy reported and the flips against the committed record printed) and exits <c>0</c> for a pass,
///     <c>1</c> for a failure and <c>2</c> when a gate set has no file; <c>--seeds</c> retrains with each evaluation
///     seed (the same settings and the same development-set choices) and reports every gate set's counts and the
///     threshold the gate's rule derives from them;
///     <c>--write</c> and <c>--check</c> produce and verify the deterministic evaluation record. The evaluation overlays
///     (<c>--set regression</c>, <c>--set generalization</c>, and the record after the data sets) are reported with and
///     without the curated cue priors of the classifier, by slice.
///     </para>
///     <para>
///     Evaluating a holdout set is the one irreversible act of model development (a set that has guided a choice no
///     longer measures anything), so only the gate, the explicit holdout sets and the record read a holdout file.
///     Exit codes: <c>0</c> success or gate passed, <c>1</c> gate failed or record stale, <c>2</c> a usage, I/O or
///     data error (a harvested row without a source document stops the evaluation). With <c>--gate</c> and
///     <c>--check</c> (or <c>--write</c>) together the exit code is the more severe of the two results, so <c>0</c>
///     needs a passing gate and a current record. The command is a pure function of its arguments, writers and directories.
///     </para>
/// </remarks>
internal static class EvaluateCommand
{
    /// <summary>The name of the command as typed on the command line.</summary>
    public const string Name = "evaluate";

    /// <summary>The number of readings listed as the worst in the console output.</summary>
    private const int WorstListLength = 15;

    /// <summary>The fewest rows a reading needs to appear in the worst list.</summary>
    private const int WorstListMinimumRows = 5;

    /// <summary>The usage text.</summary>
    public const string Usage = """
        Usage: evaluate [options]

        Measures the heteronym model on labeled sets end to end through the analyzer, with the own-part-of-speech,
        majority and first-candidate baselines, a paired document-clustered bootstrap interval of the improvement
        and a parity check of the trainer against the analyzer.

        Options:
          --data <dir>        The data folder (default: data/text-en, searched upward from the current directory).
          --model-dir <dir>   The folder of heteronyms.bin and training.json (default: the committed locations).
          --model <file>      Evaluate this model file instead of the one in the model folder.
          --set <name>        train, dev, holdout-natural, holdout-independent or all (the last three; repeatable;
                              default: dev). 'train' exists for the parity check of the training rows.
                              regression and generalization are the evaluation overlays (not part of all): they
                              are reported with and without the curated cue priors, by slice, with clustered
                              intervals, and their rows marked cue_unsolvable=yes are verified.
          --gate              Decide the statistical quality gate on its scored sets (regression, generalization,
                              holdout-natural, holdout-independent): each must reach its recorded threshold; prints
                              each set's accuracy and interval and the flips against the committed record; exit 0
                              pass, 1 fail, 2 when a gate set has no file. With --check or --write the exit code is 0
                              only when the gate passes and the record is current.
          --seeds             Also retrain with each evaluation seed and report every gate set's counts and the
                              threshold the gate's rule gives (aggregates only).
          --write             Write the evaluation record (all sets, the overlays and the seeds) to --record-dir.
          --check             Verify the evaluation record in --record-dir; exit 1 when it is stale.
          --record-dir <dir>  The record folder (default: docs/evaluation/text-en of the repository).
          --help              Shows this text.

        Exit codes: 0 = done or gate passed, 1 = gate failed or record stale, 2 = usage, I/O or data error (for
        example a harvested row without a source document in harvest-provenance.tsv, or an overlay row marked
        cue_unsolvable=yes that a curated cue can affect).
        """;

    /// <summary>
    ///     The parsed command line.
    /// </summary>
    /// <param name="Data">The data folder, or null to discover it.</param>
    /// <param name="ModelDirectory">The model directory, or null for the committed locations.</param>
    /// <param name="Model">The model file, or null.</param>
    /// <param name="Sets">The requested set names.</param>
    /// <param name="Gate">Whether to decide the quality gate.</param>
    /// <param name="Seeds">Whether to run the multi-seed study.</param>
    /// <param name="Write">Whether to write the record.</param>
    /// <param name="Check">Whether to verify the record.</param>
    /// <param name="RecordDirectory">The record folder, or null for the committed location.</param>
    /// <param name="ShowHelp">Whether usage was requested.</param>
    internal sealed record EvaluateOptions(
        string? Data,
        string? ModelDirectory,
        string? Model,
        IReadOnlyList<string> Sets,
        bool Gate,
        bool Seeds,
        bool Write,
        bool Check,
        string? RecordDirectory,
        bool ShowHelp);

    /// <summary>
    ///     Runs the command.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="output">Receives results.</param>
    /// <param name="error">Receives usage and I/O errors.</param>
    /// <param name="currentDirectory">The directory relative paths are resolved against.</param>
    /// <param name="baseDirectory">The tool's own folder, the second place the data folder is searched from.</param>
    /// <returns>The exit code: 0, 1 or 2.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, string currentDirectory, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var options = TryParse(args, out var parseError);
        if (options is null)
        {
            return UsageError(error, parseError!);
        }

        if (options.ShowHelp)
        {
            output.WriteLine(Usage);
            return 0;
        }

        try
        {
            var root = options.Data is not null
                ? Path.GetFullPath(options.Data, currentDirectory)
                : CheckDataCommand.FindDataRoot(currentDirectory) ?? CheckDataCommand.FindDataRoot(baseDirectory);
            if (root is null || !Directory.Exists(root))
            {
                return UsageError(error, $"data folder '{root ?? "data/text-en"}' was not found; use --data <dir>");
            }

            return Evaluate(options, root, currentDirectory, output);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            return UsageError(error, exception.Message);
        }
    }

    /// <summary>
    ///     Parses the arguments.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="parseError">The problem when parsing failed.</param>
    /// <returns>The options, or null when the arguments are invalid.</returns>
    internal static EvaluateOptions? TryParse(IReadOnlyList<string> args, out string? parseError)
    {
        parseError = null;
        string? data = null;
        string? modelDirectory = null;
        string? model = null;
        string? recordDirectory = null;
        var sets = new List<string>();
        bool gate = false, seeds = false, write = false, check = false, help = false;

        var position = 0;
        while (position < args.Count)
        {
            var token = args[position++];
            var separator = token.StartsWith("--", StringComparison.Ordinal) ? token.IndexOf('=') : -1;
            var name = separator > 0 ? token[..separator] : token;
            string? inline = separator > 0 ? token[(separator + 1)..] : null;

            switch (name)
            {
                case "--help" or "-h" or "-?":
                    help = true;
                    break;
                case "--gate":
                    gate = true;
                    break;
                case "--seeds":
                    seeds = true;
                    break;
                case "--write":
                    write = true;
                    break;
                case "--check":
                    check = true;
                    break;
                case "--data" or "--model-dir" or "--model" or "--set" or "--record-dir":
                    var value = inline ?? (position < args.Count ? args[position++] : null);
                    if (string.IsNullOrEmpty(value))
                    {
                        parseError = $"option {name} needs a value";
                        return null;
                    }

                    switch (name)
                    {
                        case "--data":
                            data = value;
                            break;
                        case "--model-dir":
                            modelDirectory = value;
                            break;
                        case "--model":
                            model = value;
                            break;
                        case "--record-dir":
                            recordDirectory = value;
                            break;
                        default:
                            if (value == "all")
                            {
                                sets.AddRange(HeteronymEvaluator.ReportSets);
                            }
                            else if (HeteronymEvaluator.SetFiles.ContainsKey(value) || EvaluationOverlay.IsOverlay(value))
                            {
                                sets.Add(value);
                            }
                            else
                            {
                                parseError = $"unknown set '{value}'; use train, dev, holdout-natural, holdout-independent, all, regression or generalization";
                                return null;
                            }

                            break;
                    }

                    break;
                default:
                    parseError = $"unknown option '{token}'";
                    return null;
            }
        }

        if (write && check)
        {
            parseError = "--write and --check cannot be combined";
            return null;
        }

        return new EvaluateOptions(data, modelDirectory, model, sets, gate, seeds, write, check, recordDirectory, help);
    }

    /// <summary>
    ///     Runs the evaluation and prints the results.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="root">The data folder.</param>
    /// <param name="currentDirectory">The current directory.</param>
    /// <param name="output">The output writer.</param>
    /// <returns>The exit code.</returns>
    private static int Evaluate(EvaluateOptions options, string root, string currentDirectory, TextWriter output)
    {
        // Resolve the model, the record folder and the settings.
        var repository = Path.GetFullPath(Path.Combine(root, "..", ".."));
        var modelDirectory = options.ModelDirectory is null ? null : Path.GetFullPath(options.ModelDirectory, currentDirectory);
        var modelPath = ResolveModelPath(options, modelDirectory, repository, currentDirectory);
        var recordPath = modelDirectory is not null
            ? Path.Combine(modelDirectory, TrainCommand.RecordFileName)
            : Path.Combine(repository, "tools", "DemaConsulting.Speech.Text.En.Trainer", TrainCommand.RecordFileName);
        var recordDirectory = options.RecordDirectory is null
            ? Path.Combine(repository, "docs", "evaluation", "text-en")
            : Path.GetFullPath(options.RecordDirectory, currentDirectory);

        if (!File.Exists(modelPath))
        {
            throw new InvalidDataException($"the model file {modelPath} was not found");
        }

        var trainingSettings = TrainingSettings.Default;
        var evaluationSettings = EvaluationSettings.Default;
        if (File.Exists(recordPath))
        {
            var record = TrainingRecord.Parse(File.ReadAllText(recordPath));
            trainingSettings = record.Training;
            evaluationSettings = record.Evaluation;
        }

        var image = File.ReadAllBytes(modelPath);
        var imageSha = Convert.ToHexString(SHA256.HashData(SparsePerceptronModelWriter.ToUncompressedImage(image))).ToLowerInvariant();

        // The sets to evaluate: the record needs all of them, the gate needs its scored sets.
        var wantRecord = options.Write || options.Check;
        var sets = SelectSets(options, wantRecord, root, evaluationSettings, output);

        var corpus = TrainingCorpus.Load(root);
        var evaluator = new HeteronymEvaluator(corpus, root, evaluationSettings);
        output.WriteLine($"evaluate: model {modelPath} (image sha256 {imageSha})");
        if (sets.Contains("holdout-natural", StringComparer.Ordinal) || sets.Contains("holdout-independent", StringComparer.Ordinal))
        {
            output.WriteLine("evaluate: HOLDOUT EVALUATION: a holdout set is being read");
        }

        var evaluations = new List<SetEvaluation>();
        var overlays = new List<OverlayEvaluation>();
        foreach (var set in sets)
        {
            if (EvaluationOverlay.IsOverlay(set))
            {
                var rows = EvaluationOverlay.Load(root, set);
                if (rows.Count == 0)
                {
                    if (options.Gate && IsGated(evaluationSettings, set))
                    {
                        throw new InvalidDataException($"the gate set {set} has no rows in {EvaluationOverlay.FileOf(set)}");
                    }

                    output.WriteLine($"evaluate: overlay {set}: the file has no rows; not evaluated");
                    continue;
                }

                var overlay = evaluator.EvaluateOverlay(set, rows, image);
                overlays.Add(overlay);
                evaluations.Add(overlay.Shipped);
                Print(overlay.Shipped, output);
                PrintOverlay(overlay, output);
                continue;
            }

            var evaluation = evaluator.Evaluate(set, image, withAccuracyInterval: HeteronymEvaluator.GateSets.Contains(set, StringComparer.Ordinal));
            evaluations.Add(evaluation);
            Print(evaluation, output);
        }

        var seedResults = options.Seeds || wantRecord
            ? RunSeedStudy(corpus, evaluator, trainingSettings, evaluationSettings, root, output)
            : [];

        var report = new EvaluationReport(imageSha, evaluationSettings, evaluations, seedResults, overlays);
        var exit = 0;
        if (options.Gate)
        {
            exit = PrintGate(report, EvaluationRecord.ReadCommitted(recordDirectory), output);
        }

        if (options.Write)
        {
            EvaluationRecord.Write(report, recordDirectory);
            output.WriteLine($"evaluate: wrote the evaluation record to {recordDirectory}");
        }
        else if (options.Check)
        {
            var stale = EvaluationRecord.Compare(report, recordDirectory);
            foreach (var file in stale)
            {
                output.WriteLine($"evaluate: --check: STALE: {file} is missing or differs");
            }

            output.WriteLine(stale.Count == 0 ? "evaluate: --check: OK (the evaluation record is current)" : $"evaluate: --check: FAILED ({stale.Count} files)");
            exit = Math.Max(exit, stale.Count == 0 ? 0 : 1);
        }

        return exit;
    }

    /// <summary>
    ///     Tells whether a set has a threshold in the gate settings.
    /// </summary>
    /// <param name="settings">The evaluation settings.</param>
    /// <param name="set">The set name.</param>
    /// <returns><see langword="true"/> when the gate decides on the set.</returns>
    private static bool IsGated(EvaluationSettings settings, string set) =>
        settings.GateThresholds.Any(threshold => string.Equals(threshold.Set, set, StringComparison.Ordinal));

    /// <summary>
    ///     Runs the multi-seed study: retrains with every evaluation seed and evaluates each model end to end, with the
    ///     cue priors, on every gate set that has rows, exactly as the gate scores the shipped model. Only aggregate
    ///     counts are printed, never a per-row result.
    /// </summary>
    /// <param name="corpus">The training corpus.</param>
    /// <param name="evaluator">The evaluator.</param>
    /// <param name="trainingSettings">The training settings of the record; only the seed changes.</param>
    /// <param name="evaluationSettings">The evaluation settings with the seeds and the recorded thresholds.</param>
    /// <param name="root">The data folder.</param>
    /// <param name="output">The output writer.</param>
    /// <returns>The counts of every seed.</returns>
    private static List<SeedResult> RunSeedStudy(
        TrainingCorpus corpus,
        HeteronymEvaluator evaluator,
        TrainingSettings trainingSettings,
        EvaluationSettings evaluationSettings,
        string root,
        TextWriter output)
    {
        // The gate sets with rows: the holdouts always, an overlay when its file has rows
        var overlayRows = new Dictionary<string, IReadOnlyList<OverlayRow>>(StringComparer.Ordinal);
        var sets = new List<string>();
        foreach (var set in HeteronymEvaluator.GateSets)
        {
            if (!EvaluationOverlay.IsOverlay(set))
            {
                sets.Add(set);
                continue;
            }

            if (File.Exists(Path.Combine(root, EvaluationOverlay.FileOf(set))))
            {
                var rows = EvaluationOverlay.Load(root, set);
                if (rows.Count > 0)
                {
                    overlayRows[set] = rows;
                    sets.Add(set);
                }
            }
        }

        var results = new List<SeedResult>();
        foreach (var seed in evaluationSettings.Seeds)
        {
            var trained = ModelSelection.Train(corpus, trainingSettings with { Seed = seed });
            var image = trained.CompressedImage;
            var counts = new List<SeedSetCount>();
            foreach (var set in sets)
            {
                SetEvaluation evaluation;
                if (overlayRows.TryGetValue(set, out var rows))
                {
                    var clusters = rows.ToDictionary(static row => row.Row.Id, static row => row.Cluster, StringComparer.Ordinal);
                    evaluation = evaluator.EvaluateRows(set, rows.Select(static row => row.Row).ToList(), image, row => clusters[row.Id]);
                }
                else
                {
                    evaluation = evaluator.Evaluate(set, image);
                }

                counts.Add(new SeedSetCount(set, evaluation.Rows, evaluation.Correct));
            }

            results.Add(new SeedResult(seed, counts));
            output.WriteLine($"evaluate: seed {seed}: image sha256 {trained.Result.ImageSha256}; {string.Join("; ", counts.Select(static count => $"{count.Set} {count.Correct}/{count.Rows}"))}");
        }

        if (results.Count >= 2)
        {
            foreach (var set in sets)
            {
                var counts = results.Select(result => result.Sets.First(count => count.Set == set)).ToList();
                var rule = HeteronymEvaluator.DeriveGateThreshold(counts.Select(static count => count.Correct).ToList(), counts[0].Rows);
                var recorded = evaluationSettings.GateThresholds.FirstOrDefault(threshold => threshold.Set == set);
                output.WriteLine(
                    $"evaluate: seeds {set}: correct {string.Join(", ", counts.Select(static count => count.Correct))} of {counts[0].Rows}; "
                    + $"threshold by rule {rule} bp; recorded {(recorded is null ? "none" : $"{recorded.MinAccuracyBasisPoints} bp")}");
            }
        }

        return results;
    }
    /// <summary>
    ///     Resolves the path of the model file under test.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="modelDirectory">The full model directory, or null.</param>
    /// <param name="repository">The repository root.</param>
    /// <param name="currentDirectory">The current directory.</param>
    /// <returns>The model file path.</returns>
    internal static string ResolveModelPath(EvaluateOptions options, string? modelDirectory, string repository, string currentDirectory)
    {
        if (options.Model is not null)
        {
            return Path.GetFullPath(options.Model, currentDirectory);
        }

        return modelDirectory is not null
            ? Path.Combine(modelDirectory, TrainCommand.ModelFileName)
            : Path.Combine(repository, "src", "DemaConsulting.Speech.Text.En", "Resources", TrainCommand.ModelFileName);
    }

    /// <summary>
    ///     Chooses the sets to evaluate.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="wantRecord">Whether the evaluation record is written or checked.</param>
    /// <param name="root">The data folder, where the record looks for the overlay files.</param>
    /// <param name="settings">The evaluation settings, whose thresholds name the gate sets.</param>
    /// <param name="output">The output writer, told about an overlay file the record leaves out.</param>
    /// <returns>The set names in evaluation order.</returns>
    /// <remarks>
    ///     The record takes the data sets first and the overlays after them, so the lines of the data sets in the record
    ///     files keep their place whether or not the overlays exist; an overlay whose file is absent is left out.
    /// </remarks>
    private static List<string> SelectSets(EvaluateOptions options, bool wantRecord, string root, EvaluationSettings settings, TextWriter output)
    {
        if (wantRecord)
        {
            var record = new List<string>(HeteronymEvaluator.ReportSets);
            foreach (var overlay in EvaluationOverlay.Sets)
            {
                if (File.Exists(Path.Combine(root, EvaluationOverlay.FileOf(overlay))))
                {
                    record.Add(overlay);
                }
                else if (options.Gate && IsGated(settings, overlay))
                {
                    throw new InvalidDataException($"the gate set {overlay} has no file {EvaluationOverlay.FileOf(overlay)} in the data folder");
                }
                else
                {
                    output.WriteLine($"evaluate: overlay {overlay}: {EvaluationOverlay.FileOf(overlay)} is not in the data folder; not evaluated");
                }
            }

            return record;
        }

        if (options.Gate)
        {
            // The gate's scored sets, holdouts and overlays alike, each of which must have its file
            foreach (var set in settings.GateThresholds.Select(static threshold => threshold.Set))
            {
                var file = EvaluationOverlay.IsOverlay(set) ? EvaluationOverlay.FileOf(set) : HeteronymEvaluator.SetFiles[set];
                if (!File.Exists(Path.Combine(root, file)))
                {
                    throw new InvalidDataException($"the gate set {set} has no file {file} in the data folder");
                }
            }

            return settings.GateThresholds.Select(static threshold => threshold.Set).ToList();
        }

        return options.Sets.Count > 0 ? options.Sets.Distinct().ToList() : ["dev"];
    }

    /// <summary>
    ///     Prints the evaluation of one set.
    /// </summary>
    /// <param name="set">The evaluation.</param>
    /// <param name="output">The output writer.</param>
    private static void Print(SetEvaluation set, TextWriter output)
    {
        string Percent(int correct) => EvaluationRecord.FormatPercent(HeteronymEvaluator.RoundBasisPoints(correct, set.Rows));
        output.WriteLine($"evaluate: set {set.Set}: {set.Rows} rows, {set.Aligned} aligned, {set.SingleCandidate} single-candidate, {set.Covered} covered");
        output.WriteLine($"evaluate:   model accuracy        {Percent(set.Correct)} ({set.Correct}/{set.Rows})");
        output.WriteLine($"evaluate:   own-UPOS baseline     {Percent(set.UposBaselineCorrect)} ({set.UposBaselineCorrect}/{set.Rows})");
        output.WriteLine($"evaluate:   majority baseline     {Percent(set.MajorityBaselineCorrect)} ({set.MajorityBaselineCorrect}/{set.Rows})");
        output.WriteLine($"evaluate:   first-candidate       {Percent(set.FirstBaselineCorrect)} ({set.FirstBaselineCorrect}/{set.Rows})");
        var interval = set.Interval;
        output.WriteLine($"evaluate:   improvement over own-UPOS {EvaluationRecord.FormatPercent(interval.DifferenceBasisPoints, signed: true)}, interval [{EvaluationRecord.FormatPercent(interval.LowerBasisPoints, signed: true)}, {EvaluationRecord.FormatPercent(interval.UpperBasisPoints, signed: true)}] over {interval.Clusters} clusters (lower bound above zero: {(interval.LowerIsPositive ? "yes" : "no")})");
        output.WriteLine($"evaluate:   parity: {set.Parity.Compared} compared, {set.Parity.Mismatched} mismatched, {set.Parity.Suppressed} suppressed");
        foreach (var source in set.BySource)
        {
            output.WriteLine($"evaluate:   source {source.Name}: {source.Correct}/{source.Rows} ({EvaluationRecord.FormatPercent(HeteronymEvaluator.RoundBasisPoints(source.Correct, source.Rows))}), own-UPOS {source.BaselineCorrect}");
        }

        foreach (var reading in set.ByReading.Where(static r => r.Rows >= WorstListMinimumRows).Take(WorstListLength))
        {
            output.WriteLine($"evaluate:   worst reading {reading.Name}: {reading.Correct}/{reading.Rows} ({EvaluationRecord.FormatPercent(HeteronymEvaluator.RoundBasisPoints(reading.Correct, reading.Rows))}), own-UPOS {reading.BaselineCorrect}");
        }

        foreach (var bucket in set.ByConfidence)
        {
            output.WriteLine($"evaluate:   confidence {bucket.Name}: {bucket.Correct}/{bucket.Rows}");
        }
    }

    /// <summary>
    ///     Prints the slices of an overlay with and without the curated cue priors, and the verified cue-unsolvable rows.
    /// </summary>
    /// <param name="overlay">The overlay evaluation.</param>
    /// <param name="output">The output writer.</param>
    private static void PrintOverlay(OverlayEvaluation overlay, TextWriter output)
    {
        static string Interval(BootstrapInterval interval, bool signed) =>
            $"[{EvaluationRecord.FormatPercent(interval.LowerBasisPoints, signed)}, {EvaluationRecord.FormatPercent(interval.UpperBasisPoints, signed)}]";

        var set = overlay.Shipped.Set;
        if (overlay.CueUnsolvableRows > 0)
        {
            output.WriteLine($"evaluate:   cue-unsolvable check: {overlay.CueUnsolvableRows} rows; on every one no curated cue test fires and the shipped reading equals the model-only reading");
        }

        foreach (var slice in overlay.Slices)
        {
            output.WriteLine(
                $"evaluate:   {set} slice {slice.Name}: {slice.Rows} rows, {slice.ShippedAccuracy.Clusters} clusters; "
                + $"with cues {slice.ShippedCorrect}/{slice.Rows} {EvaluationRecord.FormatPercent(slice.ShippedAccuracy.DifferenceBasisPoints)} {Interval(slice.ShippedAccuracy, false)}; "
                + $"model only {slice.ModelOnlyCorrect}/{slice.Rows} {EvaluationRecord.FormatPercent(slice.ModelOnlyAccuracy.DifferenceBasisPoints)} {Interval(slice.ModelOnlyAccuracy, false)}; "
                + $"cue effect {EvaluationRecord.FormatPercent(slice.CueEffect.DifferenceBasisPoints, signed: true)} {Interval(slice.CueEffect, true)}; "
                + $"cue fired {slice.CueFired}, changed {slice.Changed}");
        }
    }

    /// <summary>
    ///     Prints the statistical gate decision per scored set and the flip report of the model under test against the
    ///     committed record's current readings.
    /// </summary>
    /// <param name="report">The evaluation with the gate sets.</param>
    /// <param name="committed">The committed gate state of the record folder, or null when there is none.</param>
    /// <param name="output">The output writer.</param>
    /// <returns>0 when the gate passes, otherwise 1.</returns>
    /// <exception cref="InvalidDataException">A gate set was not evaluated.</exception>
    private static int PrintGate(EvaluationReport report, CommittedGate? committed, TextWriter output)
    {
        var settings = report.Evaluation;
        var verdict = HeteronymEvaluator.DecideGate(report.Sets, settings);
        foreach (var set in verdict.Sets)
        {
            output.WriteLine(
                $"evaluate: GATE {set.Set}: {set.Correct}/{set.Rows} {EvaluationRecord.FormatPercent(HeteronymEvaluator.RoundBasisPoints(set.Correct, set.Rows))}, "
                + $"{settings.ConfidencePercent}% interval [{EvaluationRecord.FormatPercent(set.Accuracy.LowerBasisPoints)}, {EvaluationRecord.FormatPercent(set.Accuracy.UpperBasisPoints)}] over {set.Accuracy.Clusters} clusters; "
                + $"threshold {EvaluationRecord.FormatPercent(set.ThresholdBasisPoints)}: {(set.Passed ? "ok" : "BELOW")}");
        }

        // The flips of the model under test against the committed current readings
        if (committed is null)
        {
            output.WriteLine("evaluate: FLIPS: no committed gate rows to compare with");
        }
        else
        {
            foreach (var set in verdict.Sets.Select(static set => set.Set))
            {
                var evaluation = report.Sets.First(evaluation => evaluation.Set == set);
                var fixedCount = 0;
                var brokenCount = 0;
                foreach (var row in (evaluation.RowReadings ?? []).OrderBy(static row => row.Id, StringComparer.Ordinal))
                {
                    var previous = committed.Rows.TryGetValue((set, row.Id), out var old) ? old.Current : "-";
                    var change = HeteronymEvaluator.ClassifyFlip(row.Gold, previous, row.Emitted);
                    if (change is null)
                    {
                        continue;
                    }

                    fixedCount += change == "fixed" ? 1 : 0;
                    brokenCount += change == "broken" ? 1 : 0;
                    output.WriteLine($"evaluate: FLIP {set} {row.Id} {row.Gold} {previous}->{row.Emitted} {change}");
                }

                output.WriteLine($"evaluate: FLIPS {set}: fixed {fixedCount}, broken {brokenCount}");
            }
        }

        output.WriteLine($"evaluate: GATE: {(verdict.Passed ? "PASS" : "FAIL")}");
        return verdict.Passed ? 0 : 1;
    }

    /// <summary>
    ///     Writes a usage error.
    /// </summary>
    /// <param name="error">The error writer.</param>
    /// <param name="message">The message.</param>
    /// <returns>The usage-error exit code, 2.</returns>
    private static int UsageError(TextWriter error, string message)
    {
        error.WriteLine($"evaluate: error: {message}");
        error.WriteLine("Run 'evaluate --help' for usage.");
        return 2;
    }
}
