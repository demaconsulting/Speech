using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;
using DemaConsulting.Speech.Sherpa.ModelManagementSubsystem;

namespace DemaConsulting.Speech.SttEvaluation;

/// <summary>
///     The parsed command line of the <c>evaluate</c> command.
/// </summary>
/// <param name="ModelIds">The models to evaluate, in order (the first is the comparison baseline).</param>
/// <param name="Compare">Whether a comparison report of the models is written.</param>
/// <param name="Check">Whether the accuracy gates are applied (exit 1 when one fails).</param>
/// <param name="Scenarios">The scenarios to run, in order.</param>
/// <param name="DataDirectory">The data folder, or <see langword="null"/> for <c>data/stt-en</c>.</param>
/// <param name="ModelsDirectory">The model store root, or <see langword="null"/> for the default per-user store.</param>
/// <param name="OutputDirectory">The output folder (inside <c>artifacts/</c>), or <see langword="null"/> for the default.</param>
/// <param name="TrailingSilenceSeconds">Digital silence fed after each recording before the flush, in seconds.</param>
/// <param name="EngineOptions">The engine options of the first model.</param>
internal sealed record EvaluateOptions(
    IReadOnlyList<string> ModelIds,
    bool Compare,
    bool Check,
    IReadOnlyList<string> Scenarios,
    string? DataDirectory,
    string? ModelsDirectory,
    string? OutputDirectory,
    double TrailingSilenceSeconds,
    EngineOptionOverrides EngineOptions);

/// <summary>
///     Entry point of the speech-to-text evaluation tool: measures word error rate, segmentation,
///     invented words and speed of installed recognition models on the committed evaluation data.
/// </summary>
/// <remarks>
///     A developer tool, never packed or shipped. It exists so recognition changes are judged by
///     numbers on a fixed, license-clean data set (<c>data/stt-en</c>) rather than by ear, and so the
///     ONNX Nemotron model can be compared with the Sherpa Nemotron model on identical audio. Exit
///     codes: <c>0</c> success (and every gate passed with <c>--check</c>), <c>1</c> a gate failed,
///     <c>2</c> a usage, data or model error.
/// </remarks>
internal static class Program
{
    /// <summary>The exit code of a successful run.</summary>
    private const int ExitSuccess = 0;

    /// <summary>The exit code of a failed accuracy gate.</summary>
    private const int ExitGateFailed = 1;

    /// <summary>The exit code of a usage, data or model error.</summary>
    private const int ExitError = 2;

    /// <summary>The usage text.</summary>
    private const string Usage = """
        Usage: DemaConsulting.Speech.SttEvaluation evaluate [options]

        Options:
          --model <id>                Model to evaluate (repeatable; default: nemotron-3.5-asr-streaming-0.6b-onnx-int4)
          --compare <id> [<id>...]    Evaluate these models on the same audio and write compare.tsv; with one id the
                                      default model is the baseline and the id is compared against it
          --check                     Apply the accuracy gates to the ONNX Nemotron model (exit 1 when one fails)
          --scenario <name>           Scenario to run (repeatable; default: all): librispeech, poem, paused, continuous, quiet
          --engine-option <Name=Value> Model parameter or ONNX Nemotron engine option of the first model (repeatable):
                                      BlankPenalty, FlushTailChunks, TrailingSilenceMs, TrailingSilenceAfterSpeechMs,
                                      MaxUtteranceMs
          --trailing-silence <s>      Digital silence fed after each recording before the flush (default 0)
          --data <dir>                Data folder (default: data/stt-en)
          --models-dir <dir>          Model store root (default: the per-user store)
          --output <dir>              Output folder inside artifacts/ (default: artifacts/stt-evaluation)
        """;

    /// <summary>
    ///     Gets the recognition models the tool can evaluate: every model registered by
    ///     <c>AddSherpaModels()</c> and <c>AddNemotronSttModels()</c>.
    /// </summary>
    private static IReadOnlyList<IRecognitionModel> KnownModels { get; } =
    [
        new OnnxNemotronMultilingualRecognitionModel(),
        new SherpaOnnxNemotronStreamingEnRecognitionModel(),
        new SherpaOnnxZipformerEnRecognitionModel(),
    ];

    /// <summary>
    ///     Runs the tool.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <returns>The exit code (see the class remarks).</returns>
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? ExitError : ExitSuccess;
        }

        try
        {
            if (args[0] != "evaluate")
            {
                throw new ArgumentException($"Unknown command '{args[0]}'.");
            }

            return Evaluate(ParseEvaluate(args.Skip(1).ToList()));
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or IOException or InvalidDataException
                                       or InvalidOperationException or UnauthorizedAccessException)
        {
            // Usage, data and model errors are reported as one line; the run cannot produce a measurement
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(Usage);
            return ExitError;
        }
    }

    /// <summary>
    ///     Parses the options of the <c>evaluate</c> command.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <returns>The parsed options.</returns>
    /// <exception cref="ArgumentException">Thrown when an option is unknown, lacks its value, or the combination is invalid.</exception>
    /// <exception cref="FormatException">Thrown when a value is malformed.</exception>
    private static EvaluateOptions ParseEvaluate(IReadOnlyList<string> args)
    {
        var models = new List<string>();
        var compared = new List<string>();
        var scenarios = new List<string>();
        var engineOptions = new List<string>();
        var compare = false;
        var check = false;
        string? data = null, modelsDir = null, output = null;
        var trailingSilence = 0.0;

        var queue = new Queue<string>(args);
        while (queue.TryDequeue(out var option))
        {
            // Takes the value that must follow the current option
            string Value() => queue.TryDequeue(out var value) ? value : throw new ArgumentException($"Option '{option}' needs a value.");

            switch (option)
            {
                case "--model":
                    models.Add(Value());
                    break;
                case "--compare":
                    compare = true;
                    while (queue.TryPeek(out var next) && !next.StartsWith("--", StringComparison.Ordinal))
                    {
                        compared.Add(queue.Dequeue());
                    }

                    break;
                case "--check":
                    check = true;
                    break;
                case "--scenario":
                    scenarios.Add(Value());
                    break;
                case "--engine-option":
                    engineOptions.Add(Value());
                    break;
                case "--trailing-silence":
                    trailingSilence = double.Parse(Value(), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--data":
                    data = Value();
                    break;
                case "--models-dir":
                    modelsDir = Value();
                    break;
                case "--output":
                    output = Value();
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{option}'.");
            }
        }

        // Validate the combination before any model is loaded
        var modelIds = ResolveModelIds(models, compare, compared);
        if (trailingSilence < 0 || !double.IsFinite(trailingSilence))
        {
            throw new ArgumentException("Option '--trailing-silence' must be a non-negative number of seconds.");
        }

        // The gates need the gated model and the gated scenarios
        if (check && !modelIds.Contains(SttEvaluationGates.GatedModelId))
        {
            throw new ArgumentException($"Option '--check' gates '{SttEvaluationGates.GatedModelId}', which is not evaluated.");
        }

        var unknown = scenarios.FirstOrDefault(s => !EvaluationScenarios.All.Contains(s));
        if (unknown is not null)
        {
            throw new ArgumentException(
                $"Unknown scenario '{unknown}'. Known scenarios: {string.Join(", ", EvaluationScenarios.All)}.");
        }

        var selected = scenarios.Count == 0 ? EvaluationScenarios.All : scenarios.Distinct(StringComparer.Ordinal).ToList();
        if (check)
        {
            selected = [.. EvaluationScenarios.Gated.Where(g => !selected.Contains(g)), .. selected];
        }

        return new EvaluateOptions(
            modelIds,
            compare,
            check,
            selected,
            data,
            modelsDir,
            output,
            trailingSilence,
            EngineOptionOverrides.Parse(engineOptions));
    }

    /// <summary>
    ///     Resolves the models to evaluate from the <c>--model</c> and <c>--compare</c> options.
    /// </summary>
    /// <param name="models">The ids given with <c>--model</c>.</param>
    /// <param name="compare">Whether <c>--compare</c> was given.</param>
    /// <param name="compared">The ids given after <c>--compare</c>.</param>
    /// <returns>
    ///     The ids in order: the <c>--compare</c> ids (with one id, the default model first and the id
    ///     second), otherwise the <c>--model</c> ids, otherwise the default model.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <c>--compare</c> has no id or an id is named twice.</exception>
    private static List<string> ResolveModelIds(List<string> models, bool compare, List<string> compared)
    {
        List<string> modelIds;
        if (compare)
        {
            if (compared.Count == 0)
            {
                throw new ArgumentException("Option '--compare' needs at least one model id.");
            }

            modelIds = compared.Count == 1 ? [SttEvaluationGates.GatedModelId, compared[0]] : compared;
        }
        else
        {
            modelIds = models.Count == 0 ? [SttEvaluationGates.GatedModelId] : models;
        }

        if (modelIds.Distinct(StringComparer.Ordinal).Count() != modelIds.Count)
        {
            throw new ArgumentException("A model is named more than once.");
        }

        return modelIds;
    }
    /// <summary>
    ///     Runs the <c>evaluate</c> command.
    /// </summary>
    /// <param name="options">The parsed options.</param>
    /// <returns>The exit code.</returns>
    /// <exception cref="ArgumentException">Thrown when a model is unknown or the output folder is outside <c>artifacts/</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a model is not installed.</exception>
    /// <exception cref="InvalidDataException">Thrown when the data does not match its manifest.</exception>
    private static int Evaluate(EvaluateOptions options)
    {
        // Locate everything first, so a missing model or bad data fails before any long run
        var root = ToolPaths.FindRepositoryRoot();
        var outputDirectory = ToolPaths.ResolveOutputDirectory(root, options.OutputDirectory);
        var data = SttDataSet.Load(options.DataDirectory ?? ToolPaths.DefaultDataDirectory(root));
        var store = new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = options.ModelsDirectory });
        var models = options.ModelIds.Select(id => ResolveInstalledModel(store, id)).ToList();
        Console.WriteLine($"Data: {data.Directory} ({data.Items.Count} files, verified)");
        Console.WriteLine($"Scenarios: {string.Join(", ", options.Scenarios)}");

        // Evaluate each model in turn; the engine options apply to the first model only
        var evaluations = new List<ModelEvaluation>();
        for (var i = 0; i < models.Count; i++)
        {
            var model = models[i];
            var overrides = i == 0 ? options.EngineOptions : EngineOptionOverrides.None;
            var evaluation = EvaluationRunner.Evaluate(
                model,
                store.GetCurrentDirectory(model.Id),
                overrides,
                data,
                options.Scenarios,
                options.TrailingSilenceSeconds,
                Console.Out);
            evaluations.Add(evaluation);
            Console.WriteLine($"Report: {EvaluationReport.WriteModel(outputDirectory, evaluation)}");
        }

        if (options.Compare)
        {
            Console.WriteLine($"Comparison: {EvaluationReport.WriteComparison(outputDirectory, evaluations)}");
        }

        EvaluationReport.WriteConsoleSummary(Console.Out, evaluations);
        return options.Check ? ReportGates(evaluations) : ExitSuccess;
    }

    /// <summary>Applies the accuracy gates to the gated model and prints each outcome.</summary>
    /// <param name="evaluations">The evaluated models (one of them is the gated model).</param>
    /// <returns><see cref="ExitSuccess"/> when every gate passed, otherwise <see cref="ExitGateFailed"/>.</returns>
    private static int ReportGates(IReadOnlyList<ModelEvaluation> evaluations)
    {
        var gated = evaluations.First(e => e.ModelId == SttEvaluationGates.GatedModelId);
        var gates = EvaluationReport.CheckGates(gated);
        Console.WriteLine();
        foreach (var gate in gates)
        {
            Console.WriteLine(
                $"Gate {gate.Scenario}: WER {EvaluationReport.Percent(gate.WordErrorRate)} <= {EvaluationReport.Percent(gate.Maximum)}: {(gate.Passed ? "PASS" : "FAIL")}");
        }

        return gates.All(g => g.Passed) ? ExitSuccess : ExitGateFailed;
    }

    /// <summary>Finds a known model by id and checks that it is installed.</summary>
    /// <param name="store">The model store.</param>
    /// <param name="id">The model id.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentException">Thrown when the id is not a known recognition model.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the model is not installed in the store.</exception>
    private static IRecognitionModel ResolveInstalledModel(SpeechModelStore store, string id)
    {
        var model = KnownModels.FirstOrDefault(m => m.Id == id)
            ?? throw new ArgumentException(
                $"'{id}' is not a known recognition model. Known models: {string.Join(", ", KnownModels.Select(m => m.Id))}.");
        if (!store.IsInstalled(id))
        {
            throw new InvalidOperationException(
                $"Model '{id}' is not installed in '{store.RootPath}'. Install it with " +
                $"'dotnet run --project tools/ModelDownloader -- {id}'.");
        }

        return model;
    }
}
