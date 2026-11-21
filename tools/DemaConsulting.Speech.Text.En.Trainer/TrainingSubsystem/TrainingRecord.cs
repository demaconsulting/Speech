using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

/// <summary>
///     The order in which the trainer visits the training rows within an epoch.
/// </summary>
/// <remarks>
///     The visiting order is part of what determines a trained model, so it is a recorded setting. The two orders
///     differ in what happens when training rows are added: a positional shuffle permutes row positions, so one
///     inserted row moves every later row to a new place in every epoch, while the keyed order gives each row a place
///     that depends only on its own id, which keeps the relative order of the existing rows.
/// </remarks>
internal enum RowVisitingOrder
{
    /// <summary>
    ///     A seeded Fisher-Yates shuffle of the row positions, applied again at the start of every epoch (the order of
    ///     the first trained models).
    /// </summary>
    Positional,

    /// <summary>
    ///     Each epoch, every visit (a row and its repeat number) is ordered by a seeded key mixed from the seed, the
    ///     epoch, a hash of the row id and the repeat number; ties go to the ordinal id, then the repeat number.
    /// </summary>
    Keyed,
}

/// <summary>
///     The settings that determine a trained model, fixed before training starts.
/// </summary>
/// <param name="Seed">The seed of the row visiting order.</param>
/// <param name="HashBits">The number of hash bits of the model, 16 to 24.</param>
/// <param name="Scale">The fixed-point scale of the quantized weights; a positive integer.</param>
/// <param name="PruneThreshold">Weights whose magnitude is at most this value are dropped from the model; 0 drops only zeros.</param>
/// <param name="NaturalWeight">How many times each natural (harvested) training row is visited per epoch; at least 1.</param>
/// <param name="MaxEpochs">The most epochs trained; the epoch count actually kept is chosen on the development set.</param>
/// <param name="TemperatureSteps">The number of candidate calibration temperatures (<c>k / divisor</c> for <c>k</c> from 1).</param>
/// <param name="TemperatureDivisor">The divisor of the candidate temperatures.</param>
/// <param name="RowOrder">The order in which the training rows are visited within an epoch.</param>
internal sealed record TrainingSettings(
    ulong Seed,
    int HashBits,
    int Scale,
    int PruneThreshold,
    int NaturalWeight,
    int MaxEpochs,
    int TemperatureSteps,
    int TemperatureDivisor,
    RowVisitingOrder RowOrder)
{
    /// <summary>The text of the positional visiting order in the record and on the command line.</summary>
    public const string PositionalOrderName = "positional";

    /// <summary>The text of the keyed visiting order in the record and on the command line.</summary>
    public const string KeyedOrderName = "keyed";

    /// <summary>Gets the settings the committed model is trained with unless an experiment option overrides them.</summary>
    /// <remarks>
    ///     The prune threshold and natural-row weight were chosen on the development set only (see RUN-LOG.md): the
    ///     pre-registered grid of prune 0, 1, 2 and natural weight 1, 2, 3 was scored by the development natural
    ///     rows decided correctly, ties going to the smaller model. The committed model was trained with the
    ///     positional visiting order; the keyed order is an experiment option until a model is trained with it.
    /// </remarks>
    public static TrainingSettings Default { get; } = new(
        Seed: 1,
        HashBits: 22,
        Scale: 64,
        PruneThreshold: 2,
        NaturalWeight: 3,
        MaxEpochs: 12,
        TemperatureSteps: 1000,
        TemperatureDivisor: 20,
        RowOrder: RowVisitingOrder.Positional);

    /// <summary>
    ///     Gets the text form of a visiting order, as written in the record and typed on the command line.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <returns><c>positional</c> or <c>keyed</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is not a defined order.</exception>
    public static string FormatRowOrder(RowVisitingOrder order) => order switch
    {
        RowVisitingOrder.Positional => PositionalOrderName,
        RowVisitingOrder.Keyed => KeyedOrderName,
        _ => throw new ArgumentOutOfRangeException(nameof(order), order, "unknown row order"),
    };

    /// <summary>
    ///     Reads the text form of a visiting order.
    /// </summary>
    /// <param name="text">The text; exactly <c>positional</c> or <c>keyed</c> (ordinal, case-sensitive). May be null.</param>
    /// <param name="order">Receives the order when the text names one.</param>
    /// <returns>Whether the text names an order.</returns>
    public static bool TryParseRowOrder(string? text, out RowVisitingOrder order)
    {
        switch (text)
        {
            case PositionalOrderName:
                order = RowVisitingOrder.Positional;
                return true;
            case KeyedOrderName:
                order = RowVisitingOrder.Keyed;
                return true;
            default:
                order = RowVisitingOrder.Positional;
                return false;
        }
    }

    /// <summary>
    ///     Checks that every setting is in its valid range.
    /// </summary>
    /// <remarks>
    ///     These ranges are independent of the corpus. Whether the scale, natural weight and epoch count are safe
    ///     for the integer averaging arithmetic depends on how many rows are trained, so that bound is checked,
    ///     with its derivation, when the trainer is constructed for a corpus.
    /// </remarks>
    /// <exception cref="InvalidDataException">A setting is out of range; the message names it.</exception>
    public void Validate()
    {
        Require(HashBits is >= 16 and <= 24, "hashBits must be 16 to 24");
        Require(Scale is >= 1 and <= 1_000_000, "scale must be 1 to 1000000");
        Require(PruneThreshold is >= 0 and <= 1_000, "pruneThreshold must be 0 to 1000");
        Require(NaturalWeight is >= 1 and <= 100, "naturalWeight must be 1 to 100");
        Require(MaxEpochs is >= 1 and <= 100, "maxEpochs must be 1 to 100");
        Require(TemperatureSteps is >= 1 and <= 100_000, "temperatureSteps must be 1 to 100000");
        Require(TemperatureDivisor is >= 1 and <= 1_000_000, "temperatureDivisor must be 1 to 1000000");
        Require(RowOrder is RowVisitingOrder.Positional or RowVisitingOrder.Keyed, "rowOrder must be positional or keyed");
    }

    /// <summary>
    ///     Throws when a condition is false.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="message">The message.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}

/// <summary>
///     The least accuracy the statistical quality gate accepts on one scored set.
/// </summary>
/// <param name="Set">The gate set: one of <see cref="HeteronymEvaluator.GateSets"/>.</param>
/// <param name="MinAccuracyBasisPoints">The least end-to-end accuracy, in basis points (9520 is 95.20%); the set passes when <c>correct × 10,000 ≥ threshold × rows</c>.</param>
internal sealed record GateThreshold(string Set, int MinAccuracyBasisPoints);

/// <summary>
///     The settings of the evaluation and the statistical quality gate.
/// </summary>
/// <param name="Seeds">The seeds the multi-seed study trains with; includes the committed seed.</param>
/// <param name="Resamples">The number of bootstrap resamples.</param>
/// <param name="ConfidencePercent">The confidence level of the bootstrap interval, in percent.</param>
/// <param name="BootstrapSeed">The seed of the bootstrap resampling.</param>
/// <param name="GateThresholds">The threshold of every scored gate set, in the ordinal order of the set names.</param>
internal sealed record EvaluationSettings(
    IReadOnlyList<ulong> Seeds,
    int Resamples,
    int ConfidencePercent,
    ulong BootstrapSeed,
    IReadOnlyList<GateThreshold> GateThresholds)
{
    /// <summary>
    ///     Gets the evaluation settings of the statistical gate.
    /// </summary>
    /// <remarks>
    ///     Each threshold is the floor of the mean minus two sample standard deviations of the correct counts of the
    ///     current recipe at the seeds 1 to 5 (<see cref="HeteronymEvaluator.DeriveGateThreshold"/>), recorded by the
    ///     trainer's <c>RUN-LOG.md</c> entry "Cleanup after PR B: the seed study and the gate thresholds".
    /// </remarks>
    public static EvaluationSettings Default { get; } = new(
        Seeds: [1UL, 2UL, 3UL, 4UL, 5UL],
        Resamples: 10_000,
        ConfidencePercent: 95,
        BootstrapSeed: 7,
        GateThresholds:
        [
            new GateThreshold("generalization", 6902),
            new GateThreshold("holdout-independent", 9483),
            new GateThreshold("holdout-natural", 9520),
            new GateThreshold("regression", 9603),
        ]);

    /// <summary>
    ///     Checks that every setting is in its valid range.
    /// </summary>
    /// <exception cref="InvalidDataException">A setting is out of range; the message names it.</exception>
    public void Validate()
    {
        if (Seeds.Count is < 1 or > 100 || Resamples is < 100 or > 1_000_000 || ConfidencePercent is < 50 or > 99)
        {
            throw new InvalidDataException("the evaluation settings are out of range");
        }

        // At least one gate set, each known, in ordinal order without repeats, with a threshold in range
        var sets = GateThresholds.Select(static threshold => threshold.Set).ToList();
        if (sets.Count == 0
            || !sets.All(set => HeteronymEvaluator.GateSets.Contains(set, StringComparer.Ordinal))
            || !sets.SequenceEqual(sets.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || GateThresholds.Any(static threshold => threshold.MinAccuracyBasisPoints is < 1 or > 10_000))
        {
            throw new InvalidDataException("the gate thresholds must name known gate sets once each, in ordinal order, with a threshold of 1 to 10000 basis points");
        }
    }

    /// <summary>
    ///     Tells whether two settings are equal value by value, including the seeds and the thresholds.
    /// </summary>
    /// <param name="other">The other settings.</param>
    /// <returns><see langword="true"/> when every value is equal.</returns>
    public bool SameValues(EvaluationSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Seeds.SequenceEqual(other.Seeds)
            && Resamples == other.Resamples
            && ConfidencePercent == other.ConfidencePercent
            && BootstrapSeed == other.BootstrapSeed
            && GateThresholds.SequenceEqual(other.GateThresholds);
    }
}

/// <summary>
///     What the training read: hashes of the inputs and the row counts the trainer saw.
/// </summary>
/// <param name="DataSha256">The SHA-256 (hex) of the canonical listing of the four input files and their hashes.</param>
/// <param name="FeaturesSha256">The SHA-256 (hex) of the ordered feature stream of the training and development rows.</param>
/// <param name="TaggerVersion">The version of the part-of-speech package whose tags are features.</param>
/// <param name="FeatureSetVersion">The feature-set version of the extractor.</param>
/// <param name="InventoryVersion">The reading-inventory revision the model is trained for.</param>
/// <param name="TrainerVersion">The version of the trainer.</param>
/// <param name="TrainRows">The rows of <c>train.tsv</c>.</param>
/// <param name="TrainUsed">The training rows used (the target is a runtime token and the form is ambiguous).</param>
/// <param name="TrainSkippedUnaligned">The training rows skipped because their target is not exactly one runtime token.</param>
/// <param name="TrainSkippedSingleCandidate">The training rows skipped because their form has one candidate reading.</param>
/// <param name="DevRows">The rows of <c>dev.tsv</c>.</param>
/// <param name="DevUsed">The development rows used for selection.</param>
/// <param name="DevSkippedUnaligned">The development rows skipped because their target is not exactly one runtime token.</param>
/// <param name="DevSkippedSingleCandidate">The development rows skipped because their form has one candidate reading.</param>
internal sealed record TrainingInputs(
    string DataSha256,
    string FeaturesSha256,
    string TaggerVersion,
    int FeatureSetVersion,
    int InventoryVersion,
    uint TrainerVersion,
    int TrainRows,
    int TrainUsed,
    int TrainSkippedUnaligned,
    int TrainSkippedSingleCandidate,
    int DevRows,
    int DevUsed,
    int DevSkippedUnaligned,
    int DevSkippedSingleCandidate);

/// <summary>
///     What training produced: the choices made on the development set and the identity of the model.
/// </summary>
/// <param name="SelectedEpochs">The number of epochs whose averaged weights were kept.</param>
/// <param name="TemperatureStep">The chosen temperature step <c>k</c>; the temperature is <c>k / divisor</c>.</param>
/// <param name="Temperature">The chosen temperature as stored in the model.</param>
/// <param name="DevNaturalCorrect">The natural development rows the kept weights decide correctly.</param>
/// <param name="DevNaturalRows">The natural development rows used for selection.</param>
/// <param name="DevAllCorrect">All development rows the kept weights decide correctly.</param>
/// <param name="DevAllRows">All development rows used for selection.</param>
/// <param name="DevNaturalNllMicro">The calibration loss on the natural development rows, in millionths, at the chosen temperature.</param>
/// <param name="WeightCount">The number of stored weights.</param>
/// <param name="ImageBytes">The size of the uncompressed model image.</param>
/// <param name="ImageSha256">The SHA-256 (hex) of the uncompressed model image.</param>
internal sealed record TrainingResult(
    int SelectedEpochs,
    int TemperatureStep,
    float Temperature,
    int DevNaturalCorrect,
    int DevNaturalRows,
    int DevAllCorrect,
    int DevAllRows,
    long DevNaturalNllMicro,
    int WeightCount,
    int ImageBytes,
    string ImageSha256);

/// <summary>
///     The training record (<c>training.json</c>): the settings a model was trained with, what the training read
///     and what it produced, in a deterministic text form the tool can verify.
/// </summary>
/// <param name="Training">The training settings.</param>
/// <param name="Evaluation">The evaluation and gate settings.</param>
/// <param name="Inputs">What the training read.</param>
/// <param name="Result">What the training produced.</param>
/// <remarks>
///     The text form has a fixed key order, two-space indentation, LF line endings and invariant-culture numbers,
///     so the same record always renders to the same bytes. It holds no time stamp, host name or duration.
/// </remarks>
internal sealed record TrainingRecord(
    TrainingSettings Training,
    EvaluationSettings Evaluation,
    TrainingInputs Inputs,
    TrainingResult Result)
{
    /// <summary>The version of the record layout; 2 holds the per-set thresholds of the statistical gate.</summary>
    public const int SchemaVersion = 2;

    /// <summary>
    ///     Renders the record as text.
    /// </summary>
    /// <returns>The JSON text, ending with a line feed.</returns>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, NewLine = "\n", IndentSize = 2 }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", SchemaVersion);

            writer.WriteStartObject("training");
            writer.WriteNumber("seed", Training.Seed);
            writer.WriteNumber("hashBits", Training.HashBits);
            writer.WriteNumber("scale", Training.Scale);
            writer.WriteNumber("pruneThreshold", Training.PruneThreshold);
            writer.WriteNumber("naturalWeight", Training.NaturalWeight);
            writer.WriteNumber("maxEpochs", Training.MaxEpochs);
            writer.WriteNumber("temperatureSteps", Training.TemperatureSteps);
            writer.WriteNumber("temperatureDivisor", Training.TemperatureDivisor);
            writer.WriteString("rowOrder", TrainingSettings.FormatRowOrder(Training.RowOrder));
            writer.WriteEndObject();

            writer.WriteStartObject("evaluation");
            writer.WriteStartArray("seeds");
            foreach (var seed in Evaluation.Seeds)
            {
                writer.WriteNumberValue(seed);
            }

            writer.WriteEndArray();
            writer.WriteNumber("resamples", Evaluation.Resamples);
            writer.WriteNumber("confidencePercent", Evaluation.ConfidencePercent);
            writer.WriteNumber("bootstrapSeed", Evaluation.BootstrapSeed);
            writer.WriteStartArray("gate");
            foreach (var threshold in Evaluation.GateThresholds)
            {
                writer.WriteStartObject();
                writer.WriteString("set", threshold.Set);
                writer.WriteNumber("minAccuracyBasisPoints", threshold.MinAccuracyBasisPoints);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteStartObject("inputs");
            writer.WriteString("dataSha256", Inputs.DataSha256);
            writer.WriteString("featuresSha256", Inputs.FeaturesSha256);
            writer.WriteString("taggerVersion", Inputs.TaggerVersion);
            writer.WriteNumber("featureSetVersion", Inputs.FeatureSetVersion);
            writer.WriteNumber("inventoryVersion", Inputs.InventoryVersion);
            writer.WriteNumber("trainerVersion", Inputs.TrainerVersion);
            writer.WriteNumber("trainRows", Inputs.TrainRows);
            writer.WriteNumber("trainUsed", Inputs.TrainUsed);
            writer.WriteNumber("trainSkippedUnaligned", Inputs.TrainSkippedUnaligned);
            writer.WriteNumber("trainSkippedSingleCandidate", Inputs.TrainSkippedSingleCandidate);
            writer.WriteNumber("devRows", Inputs.DevRows);
            writer.WriteNumber("devUsed", Inputs.DevUsed);
            writer.WriteNumber("devSkippedUnaligned", Inputs.DevSkippedUnaligned);
            writer.WriteNumber("devSkippedSingleCandidate", Inputs.DevSkippedSingleCandidate);
            writer.WriteEndObject();

            writer.WriteStartObject("result");
            writer.WriteNumber("selectedEpochs", Result.SelectedEpochs);
            writer.WriteNumber("temperatureStep", Result.TemperatureStep);
            writer.WriteString("temperature", Result.Temperature.ToString("R", CultureInfo.InvariantCulture));
            writer.WriteNumber("devNaturalCorrect", Result.DevNaturalCorrect);
            writer.WriteNumber("devNaturalRows", Result.DevNaturalRows);
            writer.WriteNumber("devAllCorrect", Result.DevAllCorrect);
            writer.WriteNumber("devAllRows", Result.DevAllRows);
            writer.WriteNumber("devNaturalNllMicro", Result.DevNaturalNllMicro);
            writer.WriteNumber("weightCount", Result.WeightCount);
            writer.WriteNumber("imageBytes", Result.ImageBytes);
            writer.WriteString("imageSha256", Result.ImageSha256);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>
    ///     Parses the text form of a record.
    /// </summary>
    /// <param name="json">The JSON text. Must not be null.</param>
    /// <returns>The record.</returns>
    /// <exception cref="InvalidDataException">The text is not a valid record of this schema, or a setting is out of range.</exception>
    public static TrainingRecord Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (Section(root, "schema").GetInt32() != SchemaVersion)
            {
                throw new InvalidDataException("the training record has an unsupported schema version");
            }

            var training = Section(root, "training");
            var evaluation = Section(root, "evaluation");
            var inputs = Section(root, "inputs");
            var result = Section(root, "result");

            var settings = new TrainingSettings(
                Section(training, "seed").GetUInt64(),
                Section(training, "hashBits").GetInt32(),
                Section(training, "scale").GetInt32(),
                Section(training, "pruneThreshold").GetInt32(),
                Section(training, "naturalWeight").GetInt32(),
                Section(training, "maxEpochs").GetInt32(),
                Section(training, "temperatureSteps").GetInt32(),
                Section(training, "temperatureDivisor").GetInt32(),
                ParseRowOrder(Section(training, "rowOrder").GetString()));
            settings.Validate();

            var seeds = Section(evaluation, "seeds").EnumerateArray().Select(static e => e.GetUInt64()).ToList();
            var thresholds = Section(evaluation, "gate").EnumerateArray()
                .Select(static e => new GateThreshold(Section(e, "set").GetString() ?? string.Empty, Section(e, "minAccuracyBasisPoints").GetInt32()))
                .ToList();
            var evaluationSettings = new EvaluationSettings(
                seeds,
                Section(evaluation, "resamples").GetInt32(),
                Section(evaluation, "confidencePercent").GetInt32(),
                Section(evaluation, "bootstrapSeed").GetUInt64(),
                thresholds);
            evaluationSettings.Validate();

            var recordedInputs = new TrainingInputs(
                Section(inputs, "dataSha256").GetString() ?? string.Empty,
                Section(inputs, "featuresSha256").GetString() ?? string.Empty,
                Section(inputs, "taggerVersion").GetString() ?? string.Empty,
                Section(inputs, "featureSetVersion").GetInt32(),
                Section(inputs, "inventoryVersion").GetInt32(),
                Section(inputs, "trainerVersion").GetUInt32(),
                Section(inputs, "trainRows").GetInt32(),
                Section(inputs, "trainUsed").GetInt32(),
                Section(inputs, "trainSkippedUnaligned").GetInt32(),
                Section(inputs, "trainSkippedSingleCandidate").GetInt32(),
                Section(inputs, "devRows").GetInt32(),
                Section(inputs, "devUsed").GetInt32(),
                Section(inputs, "devSkippedUnaligned").GetInt32(),
                Section(inputs, "devSkippedSingleCandidate").GetInt32());

            var recordedResult = new TrainingResult(
                Section(result, "selectedEpochs").GetInt32(),
                Section(result, "temperatureStep").GetInt32(),
                float.Parse(Section(result, "temperature").GetString() ?? string.Empty, NumberStyles.Float, CultureInfo.InvariantCulture),
                Section(result, "devNaturalCorrect").GetInt32(),
                Section(result, "devNaturalRows").GetInt32(),
                Section(result, "devAllCorrect").GetInt32(),
                Section(result, "devAllRows").GetInt32(),
                Section(result, "devNaturalNllMicro").GetInt64(),
                Section(result, "weightCount").GetInt32(),
                Section(result, "imageBytes").GetInt32(),
                Section(result, "imageSha256").GetString() ?? string.Empty);

            return new TrainingRecord(settings, evaluationSettings, recordedInputs, recordedResult);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            throw new InvalidDataException("the training record is not valid: " + exception.Message, exception);
        }
    }

    /// <summary>
    ///     Reads the visiting order of a record.
    /// </summary>
    /// <param name="text">The text of the <c>rowOrder</c> property.</param>
    /// <returns>The order.</returns>
    /// <exception cref="InvalidDataException">The text names no order.</exception>
    private static RowVisitingOrder ParseRowOrder(string? text) =>
        TrainingSettings.TryParseRowOrder(text, out var order)
            ? order
            : throw new InvalidDataException("rowOrder must be positional or keyed");

    /// <summary>
    ///     Gets a required property of an object.
    /// </summary>
    /// <param name="parent">The object.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The property value.</returns>
    /// <exception cref="KeyNotFoundException">The property is missing.</exception>
    private static JsonElement Section(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value)
            ? value
            : throw new KeyNotFoundException($"the property '{name}' is missing");
}
