using System.Security.Cryptography;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem;

/// <summary>
///     The <c>evaluate-external</c> command: measures the embedded heteronym model on evaluation-only external datasets
///     fetched at evaluation time from pinned revisions, and writes or verifies the external evaluation record.
/// </summary>
/// <remarks>
///     <para>
///     Without <c>--fetch</c> the command never touches the network: it verifies the cache against the pins and stops
///     (exit <c>2</c>) when the cache is missing or any file differs. Every pin, hash, format or mapping error also
///     exits <c>2</c> before any figure is printed. The cache folder must be <c>&lt;data&gt;/external</c> (git-ignored) or
///     outside the repository. External rows are only evaluated: the seed study retrains from the training and
///     development files alone, so external data can never influence a model. Console output and record files hold only
///     counts, our own reading ids, set names and pins, never a sentence, a target word or an external label. Exit codes:
///     <c>0</c> done or record current, <c>1</c> record stale, <c>2</c> usage, I/O, pin, hash or data error.
///     </para>
/// </remarks>
internal static class EvaluateExternalCommand
{
    /// <summary>The name of the command as typed on the command line.</summary>
    public const string Name = "evaluate-external";

    /// <summary>The names of the external sets in report order; <c>whd-eval</c> is the headline set.</summary>
    public static readonly IReadOnlyList<string> SetNames = ["whd-eval", "whd-train", "bench"];

    /// <summary>The usage text.</summary>
    public const string Usage = """
        Usage: evaluate-external [options]

        Evaluates the embedded heteronym model on external, evaluation-only datasets (Google WikipediaHomographData and
        heteronym-bench) fetched at evaluation time from pinned revisions. Nothing from these datasets is committed,
        embedded or used for training or for any choice; the numbers are informational and never gate a build.

        Options:
          --fetch             Download the pinned source archives that are missing or fail verification into the cache
                              (HTTPS, SHA-256 verified, published only when every pinned file matches), then evaluate.
                              Without it the network is never used.
          --set <name>        whd-eval, whd-train, bench or all (repeatable; default: all). whd-eval is the headline set.
          --cache <dir>      The cache folder (default: <data>/external). It must be that folder or outside the repository.
          --pins <file>       The pins (default: tools/DemaConsulting.Speech.Text.En.Trainer/external-sources.json;
                              the per-file pins are the sibling external-files.tsv).
          --data <dir>        The data folder (default: data/text-en, searched upward from the current directory).
          --model-dir <dir>   The folder of heteronyms.bin and training.json (default: the committed locations).
          --model <file>      Evaluate this model file instead of the one in the model folder.
          --seeds             Also retrain with each evaluation seed and report the spread and the per-seed intervals.
          --write             Write the external evaluation record (all sets and the seeds) to --record-dir.
          --check             Verify the external evaluation record in --record-dir; exit 1 when it is stale.
          --record-dir <dir>  The record folder (default: docs/evaluation/text-en/external of the repository).
          --help              Shows this text.

        Exit codes: 0 = done or record current, 1 = record stale, 2 = usage, I/O, pin, hash or data error.
        """;

    /// <summary>The number of readings listed as the worst in the console output.</summary>
    private const int WorstListLength = 15;

    /// <summary>The fewest rows a reading needs to appear in the worst list.</summary>
    private const int WorstListMinimumRows = 5;

    /// <summary>
    ///     The parsed command line.
    /// </summary>
    /// <param name="Data">The data folder, or null to discover it.</param>
    /// <param name="ModelDirectory">The model directory, or null for the committed locations.</param>
    /// <param name="Model">The model file, or null.</param>
    /// <param name="Cache">The cache folder, or null for the default.</param>
    /// <param name="Pins">The pins file, or null for the committed one.</param>
    /// <param name="RecordDirectory">The record folder, or null for the committed location.</param>
    /// <param name="Sets">The requested set names (empty means all).</param>
    /// <param name="Fetch">Whether a missing or failing cache may be downloaded.</param>
    /// <param name="Seeds">Whether to run the seed study.</param>
    /// <param name="Write">Whether to write the record.</param>
    /// <param name="Check">Whether to verify the record.</param>
    /// <param name="ShowHelp">Whether usage was requested.</param>
    internal sealed record EvaluateExternalOptions(
        string? Data,
        string? ModelDirectory,
        string? Model,
        string? Cache,
        string? Pins,
        string? RecordDirectory,
        IReadOnlyList<string> Sets,
        bool Fetch,
        bool Seeds,
        bool Write,
        bool Check,
        bool ShowHelp);

    /// <summary>
    ///     Runs the command.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="output">Receives results.</param>
    /// <param name="error">Receives usage and I/O errors.</param>
    /// <param name="currentDirectory">The directory relative paths are resolved against.</param>
    /// <param name="baseDirectory">The tool's own folder, the second place the data folder is searched from.</param>
    /// <param name="fetcher">Opens an archive over the network, or null for the default HTTPS download; the seam tests use.</param>
    /// <returns>The exit code: 0, 1 or 2.</returns>
    public static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        string currentDirectory,
        string baseDirectory,
        Func<Uri, CancellationToken, Stream>? fetcher = null)
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

            return Evaluate(options, root, currentDirectory, output, fetcher);
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
    internal static EvaluateExternalOptions? TryParse(IReadOnlyList<string> args, out string? parseError)
    {
        parseError = null;
        string? data = null;
        string? modelDirectory = null;
        string? model = null;
        string? cache = null;
        string? pins = null;
        string? recordDirectory = null;
        var sets = new List<string>();
        bool fetch = false, seeds = false, write = false, check = false, help = false;

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
                case "--fetch":
                    fetch = true;
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
                case "--data" or "--model-dir" or "--model" or "--cache" or "--pins" or "--record-dir" or "--set":
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
                        case "--cache":
                            cache = value;
                            break;
                        case "--pins":
                            pins = value;
                            break;
                        case "--record-dir":
                            recordDirectory = value;
                            break;
                        default:
                            if (value == "all")
                            {
                                sets.AddRange(SetNames);
                            }
                            else if (SetNames.Contains(value))
                            {
                                sets.Add(value);
                            }
                            else
                            {
                                parseError = $"unknown set '{value}'; use whd-eval, whd-train, bench or all";
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

        return new EvaluateExternalOptions(data, modelDirectory, model, cache, pins, recordDirectory, sets, fetch, seeds, write, check, help);
    }

    /// <summary>
    ///     Runs the evaluation and prints the results.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="root">The data folder.</param>
    /// <param name="currentDirectory">The current directory.</param>
    /// <param name="output">The output writer.</param>
    /// <param name="fetcher">The archive fetcher, or null for the default.</param>
    /// <returns>The exit code.</returns>
    private static int Evaluate(EvaluateExternalOptions options, string root, string currentDirectory, TextWriter output, Func<Uri, CancellationToken, Stream>? fetcher)
    {
        // Resolve the folders, the model and the settings.
        var repository = Path.GetFullPath(Path.Combine(root, "..", ".."));
        var defaultCache = Path.Combine(root, "external");
        var cachePath = options.Cache is null ? defaultCache : Path.GetFullPath(options.Cache, currentDirectory);
        if (!LeakageChecker.IsInside(cachePath, defaultCache) && LeakageChecker.IsInside(cachePath, repository))
        {
            throw new InvalidDataException($"the cache folder must be {defaultCache} (git-ignored) or outside the repository {repository}; refusing {cachePath}");
        }

        var pinsPath = options.Pins is null
            ? Path.Combine(repository, "tools", "DemaConsulting.Speech.Text.En.Trainer", "external-sources.json")
            : Path.GetFullPath(options.Pins, currentDirectory);
        var modelDirectory = options.ModelDirectory is null ? null : Path.GetFullPath(options.ModelDirectory, currentDirectory);
        var modelPath = EvaluateCommand.ResolveModelPath(
            new EvaluateCommand.EvaluateOptions(null, modelDirectory, options.Model, [], false, false, false, false, null, false),
            modelDirectory,
            repository,
            currentDirectory);
        var recordPath = modelDirectory is not null
            ? Path.Combine(modelDirectory, TrainCommand.RecordFileName)
            : Path.Combine(repository, "tools", "DemaConsulting.Speech.Text.En.Trainer", TrainCommand.RecordFileName);
        var recordDirectory = options.RecordDirectory is null
            ? Path.Combine(repository, "docs", "evaluation", "text-en", "external")
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

        var wantRecord = options.Write || options.Check;
        var sets = wantRecord || options.Sets.Count == 0
            ? [.. SetNames]
            : SetNames.Where(options.Sets.Contains).ToList();

        // Fail closed: the pins, the cache, the mappings and every row conversion are settled before any figure is printed.
        var pins = ExternalSourcePins.Load(pinsPath);
        var cache = new ExternalSourceCache(cachePath, fetcher);
        foreach (var id in NeededSources(sets))
        {
            cache.EnsureCached(pins.Find(id), options.Fetch);
        }

        var corpus = TrainingCorpus.Load(root);
        var converted = ConvertSets(sets, pins, cache, corpus, root);

        var image = File.ReadAllBytes(modelPath);
        var imageSha = Convert.ToHexString(SHA256.HashData(SparsePerceptronModelWriter.ToUncompressedImage(image))).ToLowerInvariant();
        output.WriteLine($"evaluate-external: model {modelPath} (image sha256 {imageSha})");
        output.WriteLine($"evaluate-external: cache {cachePath}");
        foreach (var source in NeededSources(sets).Select(pins.Find))
        {
            output.WriteLine($"evaluate-external: source {source.Id} at commit {source.Commit} ({source.Files.Count} files, {source.TotalBytes} bytes, listing sha256 {source.FilesSha256})");
        }

        var evaluator = new HeteronymEvaluator(corpus, root, evaluationSettings);
        var results = new List<ExternalSetResult>();
        foreach (var set in converted)
        {
            var evaluation = evaluator.EvaluateRows(set.Name, set.Rows, image, set.ClusterOf);
            results.Add(new ExternalSetResult(evaluation, set.Counts));
            Print(evaluation, set.Counts, output);
        }

        var seedResults = new List<ExternalSeedResult>();
        if (options.Seeds || wantRecord)
        {
            foreach (var seed in evaluationSettings.Seeds)
            {
                // The retraining reads the training and development files only; external rows are evaluated, never trained on.
                var trained = ModelSelection.Train(corpus, trainingSettings with { Seed = seed });
                var perSet = converted.Select(set => evaluator.EvaluateRowsDirect(set.Name, set.Rows, trained.CompressedImage, set.ClusterOf)).ToList();
                seedResults.Add(new ExternalSeedResult(seed, perSet));
                output.WriteLine($"evaluate-external: seed {seed}: model image sha256 {trained.Result.ImageSha256}");
                foreach (var set in perSet)
                {
                    output.WriteLine($"evaluate-external: seed {seed}: set {set.Set}: accuracy {EvaluationRecord.FormatPercent(HeteronymEvaluator.RoundBasisPoints(set.Correct, set.Rows))}, improvement over own-UPOS {EvaluationRecord.FormatPercent(set.Interval.DifferenceBasisPoints, signed: true)}, interval [{EvaluationRecord.FormatPercent(set.Interval.LowerBasisPoints, signed: true)}, {EvaluationRecord.FormatPercent(set.Interval.UpperBasisPoints, signed: true)}]");
                }
            }

            PrintSeedSummary(converted, seedResults, output);
        }

        var report = new ExternalEvaluationReport(imageSha, evaluationSettings, pins, results, seedResults);
        var exit = 0;
        if (options.Write)
        {
            ExternalEvaluationRecord.Write(report, recordDirectory);
            output.WriteLine($"evaluate-external: wrote the external evaluation record to {recordDirectory}");
        }
        else if (options.Check)
        {
            var stale = ExternalEvaluationRecord.Compare(report, recordDirectory);
            foreach (var file in stale)
            {
                output.WriteLine($"evaluate-external: --check: STALE: {file} is missing or differs");
            }

            output.WriteLine(stale.Count == 0 ? "evaluate-external: --check: OK (the external evaluation record is current)" : $"evaluate-external: --check: FAILED ({stale.Count} files)");
            exit = stale.Count == 0 ? 0 : 1;
        }

        return exit;
    }

    /// <summary>
    ///     Names the sources the sets need.
    /// </summary>
    /// <param name="sets">The set names.</param>
    /// <returns>The source ids in a fixed order.</returns>
    private static IReadOnlyList<string> NeededSources(IReadOnlyList<string> sets)
    {
        var needed = new List<string>();
        if (sets.Any(static set => set.StartsWith("whd-", StringComparison.Ordinal)))
        {
            needed.Add(WikipediaHomographAdapter.SourceId);
        }

        if (sets.Contains("bench"))
        {
            needed.Add(HeteronymBenchAdapter.SourceId);
        }

        return needed;
    }

    /// <summary>
    ///     Reads and converts the sets from the verified cache.
    /// </summary>
    /// <param name="sets">The set names.</param>
    /// <param name="pins">The pins.</param>
    /// <param name="cache">The verified cache.</param>
    /// <param name="corpus">The training corpus (inventory and training keys).</param>
    /// <param name="root">The data folder with the mapping files.</param>
    /// <returns>The converted sets in the order of <paramref name="sets"/>.</returns>
    private static List<ExternalRowSet> ConvertSets(IReadOnlyList<string> sets, ExternalSourcePins pins, ExternalSourceCache cache, TrainingCorpus corpus, string root)
    {
        var keys = ExternalRowConverter.TrainDevKeys(corpus);
        var converted = new List<ExternalRowSet>();
        foreach (var set in sets)
        {
            if (set == "bench")
            {
                var map = ReadingIdMap.Load(Path.Combine(root, "bench-mapping.tsv"), ReadingIdMap.BenchHeader, corpus.Inventory);
                var read = HeteronymBenchAdapter.Read(cache, pins.Find(HeteronymBenchAdapter.SourceId));
                converted.Add(ExternalRowConverter.Convert(set, read, map, corpus.Inventory, keys, clusterBySentence: false));
            }
            else
            {
                var map = ReadingIdMap.Load(Path.Combine(root, "whd-mapping.tsv"), ReadingIdMap.WhdHeader, corpus.Inventory);
                var read = WikipediaHomographAdapter.ReadSplit(cache, pins.Find(WikipediaHomographAdapter.SourceId), set["whd-".Length..]);
                converted.Add(ExternalRowConverter.Convert(set, read, map, corpus.Inventory, keys, clusterBySentence: true));
            }
        }

        return converted;
    }

    /// <summary>
    ///     Prints the evaluation of one set.
    /// </summary>
    /// <param name="set">The evaluation.</param>
    /// <param name="counts">The conversion accounting.</param>
    /// <param name="output">The output writer.</param>
    private static void Print(SetEvaluation set, ConversionCounts counts, TextWriter output)
    {
        string Percent(int correct) => EvaluationRecord.FormatPercent(HeteronymEvaluator.RoundBasisPoints(correct, set.Rows));
        var skipped = string.Join(", ", SkipReasons.All.Select(reason => $"{reason} {counts.Skipped.GetValueOrDefault(reason)}"));
        output.WriteLine($"evaluate-external: set {set.Set}: {counts.Read} rows read, {counts.Used} used; skipped: {skipped}");
        output.WriteLine($"evaluate-external:   {counts.NonAsciiRows} non-ASCII rows, {counts.OffsetsConverted} offsets converted, {counts.OffsetsRepaired} repaired, {counts.ExactOverlap} sentences equal to a training or development sentence (kept)");
        output.WriteLine($"evaluate-external:   {set.Aligned} aligned, {set.SingleCandidate} single-candidate, {set.Covered} covered");
        var accuracy = set.AccuracyInterval ?? new BootstrapInterval(0, 0, 0, 0, false);
        output.WriteLine($"evaluate-external:   model accuracy        {Percent(set.Correct)} ({set.Correct}/{set.Rows}), interval [{EvaluationRecord.FormatPercent(accuracy.LowerBasisPoints)}, {EvaluationRecord.FormatPercent(accuracy.UpperBasisPoints)}] over {accuracy.Clusters} clusters");
        output.WriteLine($"evaluate-external:   own-UPOS baseline     {Percent(set.UposBaselineCorrect)} ({set.UposBaselineCorrect}/{set.Rows})");
        output.WriteLine($"evaluate-external:   majority baseline     {Percent(set.MajorityBaselineCorrect)} ({set.MajorityBaselineCorrect}/{set.Rows})");
        output.WriteLine($"evaluate-external:   always-default (first-candidate) {Percent(set.FirstBaselineCorrect)} ({set.FirstBaselineCorrect}/{set.Rows})");
        var interval = set.Interval;
        output.WriteLine($"evaluate-external:   improvement over own-UPOS {EvaluationRecord.FormatPercent(interval.DifferenceBasisPoints, signed: true)}, interval [{EvaluationRecord.FormatPercent(interval.LowerBasisPoints, signed: true)}, {EvaluationRecord.FormatPercent(interval.UpperBasisPoints, signed: true)}] (lower bound above zero: {(interval.LowerIsPositive ? "yes" : "no")})");
        output.WriteLine($"evaluate-external:   parity: {set.Parity.Compared} compared, {set.Parity.Mismatched} mismatched, {set.Parity.Suppressed} suppressed");
        foreach (var source in set.BySource)
        {
            output.WriteLine($"evaluate-external:   stratum {source.Name}: {source.Correct}/{source.Rows} ({EvaluationRecord.FormatPercent(HeteronymEvaluator.RoundBasisPoints(source.Correct, source.Rows))}), own-UPOS {source.BaselineCorrect}");
        }

        foreach (var reading in set.ByReading.Where(static r => r.Rows >= WorstListMinimumRows).Take(WorstListLength))
        {
            output.WriteLine($"evaluate-external:   worst reading {reading.Name}: {reading.Correct}/{reading.Rows} ({EvaluationRecord.FormatPercent(HeteronymEvaluator.RoundBasisPoints(reading.Correct, reading.Rows))}), own-UPOS {reading.BaselineCorrect}");
        }

        foreach (var bucket in set.ByConfidence)
        {
            output.WriteLine($"evaluate-external:   confidence {bucket.Name}: {bucket.Correct}/{bucket.Rows}");
        }
    }

    /// <summary>
    ///     Prints the mean, minimum and maximum accuracy of each set over the seeds.
    /// </summary>
    /// <param name="sets">The converted sets.</param>
    /// <param name="seeds">The seed results.</param>
    /// <param name="output">The output writer.</param>
    private static void PrintSeedSummary(IReadOnlyList<ExternalRowSet> sets, IReadOnlyList<ExternalSeedResult> seeds, TextWriter output)
    {
        foreach (var name in sets.Select(static set => set.Name))
        {
            var accuracy = seeds
                .Select(seed => seed.Sets.First(s => s.Set == name))
                .Select(static s => HeteronymEvaluator.RoundBasisPoints(s.Correct, s.Rows))
                .ToList();
            if (accuracy.Count > 0)
            {
                output.WriteLine($"evaluate-external: seeds {name}: mean {EvaluationRecord.FormatPercent(EvaluationRecord.Mean(accuracy))}, min {EvaluationRecord.FormatPercent(accuracy.Min())}, max {EvaluationRecord.FormatPercent(accuracy.Max())}");
            }
        }
    }

    /// <summary>
    ///     Writes a usage error.
    /// </summary>
    /// <param name="error">The error writer.</param>
    /// <param name="message">The message.</param>
    /// <returns>The usage-error exit code, 2.</returns>
    private static int UsageError(TextWriter error, string message)
    {
        error.WriteLine($"evaluate-external: error: {message}");
        error.WriteLine("Run 'evaluate-external --help' for usage.");
        return 2;
    }
}
