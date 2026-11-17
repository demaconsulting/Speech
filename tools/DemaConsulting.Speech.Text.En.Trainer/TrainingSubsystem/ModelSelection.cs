using System.Security.Cryptography;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

/// <summary>
///     One trained model: its identity, the choices made on the development set, and its images.
/// </summary>
/// <param name="Inputs">What the training read.</param>
/// <param name="Result">What the training produced.</param>
/// <param name="UncompressedImage">The model file with an uncompressed payload, the representation that is hashed and compared.</param>
/// <param name="CompressedImage">The model file with a Deflate payload, the representation that is committed.</param>
/// <param name="Epochs">The development accuracy after each trained epoch.</param>
internal sealed record TrainedModel(
    TrainingInputs Inputs,
    TrainingResult Result,
    byte[] UncompressedImage,
    byte[] CompressedImage,
    IReadOnlyList<EpochScore> Epochs);

/// <summary>
///     The development accuracy of the deployable weights after one epoch.
/// </summary>
/// <param name="Epoch">The epoch number, from 1.</param>
/// <param name="NaturalCorrect">The natural development rows decided correctly.</param>
/// <param name="AllCorrect">All development rows decided correctly.</param>
internal readonly record struct EpochScore(int Epoch, int NaturalCorrect, int AllCorrect);

/// <summary>
///     Chooses, on the development set only, how many epochs to keep and which calibration temperature to store,
///     and builds the model image from the choice.
/// </summary>
/// <remarks>
///     <para>
///     <b>Epochs.</b> After every epoch the final deployable weights (averaged, quantized, pruned) are scored on the
///     development rows with the runtime's scoring; the kept epoch is the one that decides the most natural
///     (harvested) development rows correctly, and an exact tie keeps the earlier epoch. When the development set
///     has no natural row, all development rows are used instead. Rows that are not aligned to the runtime
///     tokenization, and rows of a one-candidate form, carry no signal and are left out.
///     </para>
///     <para>
///     <b>Temperature.</b> The calibration temperature is the grid value <c>k / divisor</c> that minimizes the
///     negative log-likelihood of the same rows, with a tie going to the smaller <c>k</c>. The loss is computed from
///     the integer scores with the trainer's own <see cref="DeterministicMath"/>, so the choice does not depend on a
///     platform's math library, and only the chosen <see cref="float"/> reaches the image.
///     </para>
///     <para>
///     No holdout set is read here; the corpus does not contain one. Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class ModelSelection
{
    /// <summary>The version of the trainer, recorded in the model header.</summary>
    public const uint TrainerVersion = 1;

    /// <summary>
    ///     Trains a model and makes every development-set choice.
    /// </summary>
    /// <param name="corpus">The corpus. Must not be null.</param>
    /// <param name="settings">The settings. Must not be null and valid.</param>
    /// <returns>The trained model.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidDataException">The settings are out of range.</exception>
    public static TrainedModel Train(TrainingCorpus corpus, TrainingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        var inventory = corpus.Inventory;
        var mask = (uint)((1L << settings.HashBits) - 1);
        var dev = corpus.Dev.Where(static row => row.IsAligned && row.IsAmbiguous).ToList();
        var hasNatural = dev.Any(static row => row.Row.IsNatural);

        // Train, scoring the deployable weights of every epoch on the development rows.
        var epochs = new List<EpochScore>();
        short[]? best = null;
        var bestEpoch = 0;
        var bestSelection = -1;
        var trainer = new AveragedPerceptronTrainer(corpus, settings);
        trainer.Train(epoch =>
        {
            var natural = 0;
            var all = 0;
            Span<int> scores = stackalloc int[ReadingInventory.MaxCandidates];
            foreach (var row in dev)
            {
                var decided = AveragedPerceptronTrainer.Decide(inventory, row, new ShortWeights(epoch.Weights), mask, scores);
                if (row.Candidates[decided] != row.LabelIndex)
                {
                    continue;
                }

                all++;
                if (row.Row.IsNatural)
                {
                    natural++;
                }
            }

            epochs.Add(new EpochScore(epoch.Epoch, natural, all));
            var selection = hasNatural ? natural : all;
            if (selection > bestSelection)
            {
                bestSelection = selection;
                bestEpoch = epoch.Epoch;
                best = (short[])epoch.Weights.Clone();
            }
        });

        var weights = best ?? throw new InvalidOperationException("No epoch was trained.");

        // Calibrate on the rows the epoch was selected on.
        var calibration = hasNatural ? dev.Where(static row => row.Row.IsNatural).ToList() : dev;
        var (step, nllMicro) = ChooseTemperature(inventory, calibration, weights, mask, settings);
        var temperature = (float)step / settings.TemperatureDivisor;

        // Build both images from the kept weights.
        var pairs = new List<(uint Bucket, short Weight)>();
        for (var bucket = 0; bucket < weights.Length; bucket++)
        {
            if (weights[bucket] != 0)
            {
                pairs.Add(((uint)bucket, weights[bucket]));
            }
        }

        var uncompressed = SparsePerceptronModelWriter.Compile(settings.HashBits, ReadingInventory.CurrentVersion, temperature, settings.Scale, pairs, compress: false, TrainerVersion, corpus.DataSha256Bytes);
        var compressed = SparsePerceptronModelWriter.Compile(settings.HashBits, ReadingInventory.CurrentVersion, temperature, settings.Scale, pairs, compress: true, TrainerVersion, corpus.DataSha256Bytes);

        var devNatural = dev.Count(static row => row.Row.IsNatural);
        var final = epochs[bestEpoch - 1];
        var inputs = BuildInputs(corpus);
        var result = new TrainingResult(
            bestEpoch,
            step,
            temperature,
            final.NaturalCorrect,
            devNatural,
            final.AllCorrect,
            dev.Count,
            nllMicro,
            pairs.Count,
            uncompressed.Length,
            Convert.ToHexString(SHA256.HashData(uncompressed)).ToLowerInvariant());
        return new TrainedModel(inputs, result, uncompressed, compressed, epochs);
    }

    /// <summary>
    ///     Summarizes what a corpus holds.
    /// </summary>
    /// <param name="corpus">The corpus.</param>
    /// <returns>The inputs record.</returns>
    public static TrainingInputs BuildInputs(TrainingCorpus corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        return new TrainingInputs(
            corpus.DataSha256,
            corpus.FeaturesSha256,
            TrainingCorpus.TaggerVersion,
            HeteronymFeatureExtractor.FeatureSetVersion,
            ReadingInventory.CurrentVersion,
            TrainerVersion,
            corpus.Train.Count,
            corpus.Train.Count(static row => row.IsAligned && row.IsAmbiguous),
            corpus.Train.Count(static row => !row.IsAligned),
            corpus.Train.Count(static row => row.IsAligned && !row.IsAmbiguous),
            corpus.Dev.Count,
            corpus.Dev.Count(static row => row.IsAligned && row.IsAmbiguous),
            corpus.Dev.Count(static row => !row.IsAligned),
            corpus.Dev.Count(static row => row.IsAligned && !row.IsAmbiguous));
    }

    /// <summary>
    ///     Finds the grid temperature that minimizes the negative log-likelihood of the rows.
    /// </summary>
    /// <param name="inventory">The inventory.</param>
    /// <param name="rows">The calibration rows.</param>
    /// <param name="weights">The kept weights.</param>
    /// <param name="mask">The bucket mask.</param>
    /// <param name="settings">The settings that define the grid.</param>
    /// <returns>The chosen step <c>k</c> and the loss at it in millionths.</returns>
    private static (int Step, long NllMicro) ChooseTemperature(ReadingInventory inventory, List<PreparedRow> rows, short[] weights, uint mask, TrainingSettings settings)
    {
        // Without rows there is nothing to calibrate on: keep a temperature of one.
        if (rows.Count == 0)
        {
            return (settings.TemperatureDivisor, 0);
        }

        // The integer scores of every row, once.
        var scoreRows = new int[rows.Count][];
        var gold = new int[rows.Count];
        var weightSource = new ShortWeights(weights);
        Span<int> scores = stackalloc int[ReadingInventory.MaxCandidates];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            AveragedPerceptronTrainer.Decide(inventory, row, weightSource, mask, scores);
            scoreRows[i] = scores[..row.Candidates.Count].ToArray();
            gold[i] = row.Candidates.ToList().IndexOf(row.LabelIndex);
        }

        var bestStep = 1;
        var bestLoss = double.PositiveInfinity;
        for (var step = 1; step <= settings.TemperatureSteps; step++)
        {
            var temperature = (float)step / settings.TemperatureDivisor;
            var divisor = (double)settings.Scale * temperature;
            var loss = 0.0;
            for (var i = 0; i < scoreRows.Length; i++)
            {
                loss += RowLoss(scoreRows[i], gold[i], divisor);
            }

            if (loss < bestLoss)
            {
                bestLoss = loss;
                bestStep = step;
            }
        }

        return (bestStep, (long)Math.Round(bestLoss * 1_000_000.0, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    ///     Computes the negative log-likelihood of the labeled candidate of one row at a temperature divisor.
    /// </summary>
    /// <param name="scores">The integer scores of the candidates.</param>
    /// <param name="gold">The position of the labeled candidate.</param>
    /// <param name="divisor">The scale times the temperature.</param>
    /// <returns>The loss.</returns>
    private static double RowLoss(int[] scores, int gold, double divisor)
    {
        var top = scores[0];
        foreach (var score in scores)
        {
            top = Math.Max(top, score);
        }

        var sum = 0.0;
        foreach (var score in scores)
        {
            sum += DeterministicMath.Exp((score - top) / divisor);
        }

        return DeterministicMath.Log(sum) - ((scores[gold] - top) / divisor);
    }
}
