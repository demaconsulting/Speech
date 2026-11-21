using System.Diagnostics;
using System.Globalization;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

/// <summary>
///     The <c>train</c> command: trains the heteronym model from the training data, and with <c>--check</c> verifies
///     that the committed model and its record are what retraining produces.
/// </summary>
/// <remarks>
///     <para>
///     <b>Train.</b> Reads only the training and development data and the inventory, chooses the epoch count and
///     the calibration temperature on the development set, and writes the model file (Deflate-compressed) and the
///     training record (<c>training.json</c>). Without <c>--model-dir</c> the committed locations are written;
///     experiment options (seed, hash bits, pruning, natural-row weight, epoch count, row visiting order) change the
///     settings and therefore require <c>--model-dir</c>, so an experiment can never overwrite the committed files.
///     </para>
///     <para>
///     <b>Check.</b> Retrains with the settings in the committed record and compares everything that could make the
///     committed model stale: the inputs (data, feature stream, tagger, feature set, inventory), the choices and the
///     image hash recorded in the record, and the committed model file itself as an uncompressed image (the
///     compressed bytes depend on the runtime's Deflate implementation and are never compared). Every difference is
///     named.
///     </para>
///     <para>
///     Exit codes: <c>0</c> success (or a current model), <c>1</c> a stale or missing committed model or record,
///     <c>2</c> a usage or I/O error or a data error. The command is a pure function of its arguments, writers and
///     directories, so tests run it in memory.
///     </para>
/// </remarks>
internal static class TrainCommand
{
    /// <summary>The name of the command as typed on the command line.</summary>
    public const string Name = "train";

    /// <summary>The file name of the model in a model directory.</summary>
    public const string ModelFileName = "heteronyms.bin";

    /// <summary>The file name of the record in a model directory.</summary>
    public const string RecordFileName = "training.json";

    /// <summary>The usage text.</summary>
    public const string Usage = """
        Usage: train [options]
               train --check [options]

        Trains the heteronym model from train.tsv and dev.tsv (the holdout sets are never read), or with
        --check verifies that the committed model and training record are what retraining produces.

        Options:
          --data <dir>            The data folder (default: data/text-en, searched upward from the current directory).
          --model-dir <dir>       The folder of heteronyms.bin and training.json (default: the committed locations).
          --check                 Verify instead of write; exit 1 when the model or record is stale.
          --seed <n>              Experiment: the shuffle seed.
          --hash-bits <n>         Experiment: the number of hash bits (16 to 24).
          --prune <n>             Experiment: drop weights of magnitude at most n.
          --natural-weight <n>    Experiment: visit each natural row n times per epoch.
          --max-epochs <n>        Experiment: the most epochs trained.
          --row-order <order>     Experiment: the row visiting order, positional (a seeded shuffle of the row
                                  positions; the default) or keyed (a seeded key per row id, so added rows keep
                                  the relative order of the existing ones).
          --help                  Shows this text.

        Experiment options require --model-dir and cannot be combined with --check.
        Exit codes: 0 = trained or current, 1 = stale, 2 = usage, I/O or data error.
        """;

    /// <summary>
    ///     Runs the command.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="output">Receives progress and results.</param>
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

            var (modelPath, recordPath) = Locations(options, root, currentDirectory);
            return options.Verify
                ? Check(root, modelPath, recordPath, output)
                : TrainAndWrite(options, root, modelPath, recordPath, output);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            return UsageError(error, exception.Message);
        }
    }

    /// <summary>
    ///     The parsed command line.
    /// </summary>
    /// <param name="Data">The data folder, or null to discover it.</param>
    /// <param name="ModelDirectory">The model directory, or null for the committed locations.</param>
    /// <param name="Verify">Whether to verify instead of write.</param>
    /// <param name="Overrides">The experiment overrides applied to the default settings; empty for none.</param>
    /// <param name="RowOrder">The experiment override of the row visiting order, or null for the default.</param>
    /// <param name="ShowHelp">Whether usage was requested.</param>
    internal sealed record TrainOptions(string? Data, string? ModelDirectory, bool Verify, IReadOnlyDictionary<string, long> Overrides, RowVisitingOrder? RowOrder, bool ShowHelp);

    /// <summary>
    ///     Parses the arguments.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="parseError">The problem when parsing failed.</param>
    /// <returns>The options, or null when the arguments are invalid.</returns>
    internal static TrainOptions? TryParse(IReadOnlyList<string> args, out string? parseError)
    {
        parseError = null;
        string? data = null;
        string? modelDirectory = null;
        var check = false;
        var help = false;
        RowVisitingOrder? rowOrder = null;
        var overrides = new Dictionary<string, long>(StringComparer.Ordinal);

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
                case "--check":
                    check = true;
                    break;
                case "--data" or "--model-dir" or "--seed" or "--hash-bits" or "--prune" or "--natural-weight" or "--max-epochs" or "--row-order":
                    var value = inline ?? (position < args.Count ? args[position++] : null);
                    if (string.IsNullOrEmpty(value))
                    {
                        parseError = $"option {name} needs a value";
                        return null;
                    }

                    if (name == "--data")
                    {
                        data = value;
                    }
                    else if (name == "--model-dir")
                    {
                        modelDirectory = value;
                    }
                    else if (name == "--row-order")
                    {
                        if (!TrainingSettings.TryParseRowOrder(value, out var order))
                        {
                            parseError = $"option {name} needs {TrainingSettings.PositionalOrderName} or {TrainingSettings.KeyedOrderName}";
                            return null;
                        }

                        rowOrder = order;
                    }
                    else if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                    {
                        overrides[name] = number;
                    }
                    else
                    {
                        parseError = $"option {name} needs a non-negative integer";
                        return null;
                    }

                    break;
                default:
                    parseError = $"unknown option '{token}'";
                    return null;
            }
        }

        if (!help && (overrides.Count > 0 || rowOrder is not null) && (modelDirectory is null || check))
        {
            parseError = "experiment options need --model-dir and cannot be combined with --check";
            return null;
        }

        return new TrainOptions(data, modelDirectory, check, overrides, rowOrder, help);
    }

    /// <summary>
    ///     Resolves the model and record paths.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="dataRoot">The data folder.</param>
    /// <param name="currentDirectory">The current directory.</param>
    /// <returns>The model file path and the record file path.</returns>
    private static (string Model, string Record) Locations(TrainOptions options, string dataRoot, string currentDirectory)
    {
        if (options.ModelDirectory is not null)
        {
            var directory = Path.GetFullPath(options.ModelDirectory, currentDirectory);
            return (Path.Combine(directory, ModelFileName), Path.Combine(directory, RecordFileName));
        }

        // The committed locations, relative to the repository that holds data/text-en.
        var repository = Path.GetFullPath(Path.Combine(dataRoot, "..", ".."));
        return (
            Path.Combine(repository, "src", "DemaConsulting.Speech.Text.En", "Resources", ModelFileName),
            Path.Combine(repository, "tools", "DemaConsulting.Speech.Text.En.Trainer", RecordFileName));
    }

    /// <summary>
    ///     Applies the experiment overrides to the default settings.
    /// </summary>
    /// <param name="overrides">The overrides by option name.</param>
    /// <param name="rowOrder">The row visiting order override, or null for the default.</param>
    /// <returns>The settings.</returns>
    private static TrainingSettings Apply(IReadOnlyDictionary<string, long> overrides, RowVisitingOrder? rowOrder)
    {
        var settings = TrainingSettings.Default;
        if (rowOrder is { } order)
        {
            settings = settings with { RowOrder = order };
        }

        foreach (var (name, value) in overrides)
        {
            settings = name switch
            {
                "--seed" => settings with { Seed = (ulong)value },
                "--hash-bits" => settings with { HashBits = (int)Math.Min(value, int.MaxValue) },
                "--prune" => settings with { PruneThreshold = (int)Math.Min(value, int.MaxValue) },
                "--natural-weight" => settings with { NaturalWeight = (int)Math.Min(value, int.MaxValue) },
                _ => settings with { MaxEpochs = (int)Math.Min(value, int.MaxValue) },
            };
        }

        settings.Validate();
        return settings;
    }

    /// <summary>
    ///     Trains and writes the model and record.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="root">The data folder.</param>
    /// <param name="modelPath">The model file to write.</param>
    /// <param name="recordPath">The record file to write.</param>
    /// <param name="output">The output writer.</param>
    /// <returns>The exit code, 0.</returns>
    private static int TrainAndWrite(TrainOptions options, string root, string modelPath, string recordPath, TextWriter output)
    {
        var settings = Apply(options.Overrides, options.RowOrder);

        // An existing record keeps its evaluation and gate settings, so retraining never moves the gate.
        var evaluation = File.Exists(recordPath) ? TrainingRecord.Parse(File.ReadAllText(recordPath)).Evaluation : EvaluationSettings.Default;

        var stopwatch = Stopwatch.StartNew();
        var corpus = TrainingCorpus.Load(root);
        var loaded = stopwatch.Elapsed;
        output.WriteLine($"train: data folder {root}");
        output.WriteLine($"train: {corpus.Train.Count} training rows, {corpus.Dev.Count} development rows prepared in {loaded.TotalSeconds:F1} s");
        output.WriteLine($"train: seed {settings.Seed}, row order {TrainingSettings.FormatRowOrder(settings.RowOrder)}");

        stopwatch.Restart();
        var trained = ModelSelection.Train(corpus, settings);
        var elapsed = stopwatch.Elapsed;

        var record = new TrainingRecord(settings, evaluation, trained.Inputs, trained.Result);
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(recordPath)!);
        File.WriteAllBytes(modelPath, trained.CompressedImage);
        File.WriteAllText(recordPath, record.ToJson(), new System.Text.UTF8Encoding(false));

        foreach (var epoch in trained.Epochs)
        {
            output.WriteLine($"train: epoch {epoch.Epoch}: dev natural {epoch.NaturalCorrect}/{trained.Result.DevNaturalRows}, dev all {epoch.AllCorrect}/{trained.Result.DevAllRows}");
        }

        var result = trained.Result;
        output.WriteLine($"train: kept {result.SelectedEpochs} epochs, temperature {result.Temperature.ToString("R", CultureInfo.InvariantCulture)} (step {result.TemperatureStep})");
        output.WriteLine($"train: {result.WeightCount} weights, model {trained.CompressedImage.Length} bytes compressed, {result.ImageBytes} bytes uncompressed");
        output.WriteLine($"train: image sha256 {result.ImageSha256}");
        output.WriteLine($"train: training and selection took {elapsed.TotalSeconds:F1} s");
        output.WriteLine($"train: wrote {modelPath} and {recordPath}");
        return 0;
    }

    /// <summary>
    ///     Retrains and compares with the committed model and record.
    /// </summary>
    /// <param name="root">The data folder.</param>
    /// <param name="modelPath">The committed model file.</param>
    /// <param name="recordPath">The committed record file.</param>
    /// <param name="output">The output writer.</param>
    /// <returns>The exit code: 0 when current, 1 when stale.</returns>
    private static int Check(string root, string modelPath, string recordPath, TextWriter output)
    {
        var stale = new List<string>();
        if (!File.Exists(recordPath))
        {
            output.WriteLine($"train: --check: STALE: the training record {recordPath} is missing");
            return 1;
        }

        TrainingRecord record;
        try
        {
            record = TrainingRecord.Parse(File.ReadAllText(recordPath));
        }
        catch (InvalidDataException exception)
        {
            output.WriteLine($"train: --check: STALE: {exception.Message}");
            return 1;
        }

        // Retrain with the committed settings and compare every input and every choice.
        var corpus = TrainingCorpus.Load(root);
        var fresh = ModelSelection.Train(corpus, record.Training);
        CompareInputs(record.Inputs, fresh.Inputs, stale);
        CompareResult(record.Result, fresh.Result, stale);

        // The committed model, as an uncompressed image, must equal the retrained one.
        if (!File.Exists(modelPath))
        {
            stale.Add($"the model file {modelPath} is missing");
        }
        else
        {
            try
            {
                var committed = SparsePerceptronModelWriter.ToUncompressedImage(File.ReadAllBytes(modelPath));
                if (!committed.AsSpan().SequenceEqual(fresh.UncompressedImage))
                {
                    stale.Add("the committed model file differs from the retrained model (weights differ)");
                }
            }
            catch (InvalidDataException exception)
            {
                stale.Add($"the committed model file is not a valid model: {exception.Message}");
            }
        }

        foreach (var reason in stale)
        {
            output.WriteLine($"train: --check: STALE: {reason}");
        }

        output.WriteLine(stale.Count == 0
            ? "train: --check: OK (the committed model and record are what retraining produces)"
            : $"train: --check: FAILED ({stale.Count} differences)");
        return stale.Count == 0 ? 0 : 1;
    }

    /// <summary>
    ///     Names every difference between the recorded inputs and the current ones.
    /// </summary>
    /// <param name="recorded">The recorded inputs.</param>
    /// <param name="current">The current inputs.</param>
    /// <param name="stale">Receives a message per difference.</param>
    private static void CompareInputs(TrainingInputs recorded, TrainingInputs current, List<string> stale)
    {
        if (recorded.DataSha256 != current.DataSha256)
        {
            stale.Add("the training data changed (dataSha256 differs)");
        }

        if (recorded.TaggerVersion != current.TaggerVersion)
        {
            stale.Add($"the part-of-speech tagger changed (recorded {recorded.TaggerVersion}, now {current.TaggerVersion})");
        }

        if (recorded.FeatureSetVersion != current.FeatureSetVersion || recorded.InventoryVersion != current.InventoryVersion || recorded.TrainerVersion != current.TrainerVersion)
        {
            stale.Add("the feature set, inventory or trainer version changed");
        }

        if (recorded.FeaturesSha256 != current.FeaturesSha256)
        {
            stale.Add("the feature stream changed (featuresSha256 differs: the tagger or the feature extractor drifted)");
        }

        var recordedCounts = (recorded.TrainRows, recorded.TrainUsed, recorded.TrainSkippedUnaligned, recorded.TrainSkippedSingleCandidate, recorded.DevRows, recorded.DevUsed, recorded.DevSkippedUnaligned, recorded.DevSkippedSingleCandidate);
        var currentCounts = (current.TrainRows, current.TrainUsed, current.TrainSkippedUnaligned, current.TrainSkippedSingleCandidate, current.DevRows, current.DevUsed, current.DevSkippedUnaligned, current.DevSkippedSingleCandidate);
        if (recordedCounts != currentCounts)
        {
            stale.Add("the recorded row counts differ from the current ones");
        }
    }

    /// <summary>
    ///     Names every difference between the recorded result and the retrained one.
    /// </summary>
    /// <param name="recorded">The recorded result.</param>
    /// <param name="current">The retrained result.</param>
    /// <param name="stale">Receives a message per difference.</param>
    private static void CompareResult(TrainingResult recorded, TrainingResult current, List<string> stale)
    {
        if (recorded.SelectedEpochs != current.SelectedEpochs || recorded.TemperatureStep != current.TemperatureStep)
        {
            stale.Add("the choices made on the development set differ (epochs or temperature)");
        }

        if (!string.Equals(recorded.ImageSha256, current.ImageSha256, StringComparison.Ordinal))
        {
            stale.Add("the recorded image hash differs from the retrained model (weights differ)");
        }
        else if (recorded != current)
        {
            stale.Add("the recorded result differs from the retrained one");
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
        error.WriteLine($"train: error: {message}");
        error.WriteLine("Run 'train --help' for usage.");
        return 2;
    }
}
